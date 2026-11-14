import os
from pathlib import Path
import subprocess
import tempfile
import unittest


WORKFLOW = Path(__file__).resolve().parents[1] / ".github" / "workflows" / "main.yml"


def workflow_step(name):
    text = WORKFLOW.read_text(encoding="utf-8")
    start = text.index(f"      - name: {name}\n")
    end = text.find("\n      - name:", start + 1)
    body = text[start : end if end != -1 else None].split("        run: |\n", 1)[1]
    return "\n".join(line[10:] for line in body.splitlines() if line.startswith("          "))


class DeploymentFirewallTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        bin_dir = self.root / "bin"
        bin_dir.mkdir()
        mocks = {
            "curl": """#!/bin/bash
if [[ "$*" == *api.ipify.org* ]]; then echo "${MOCK_IP:-8.8.8.8}"; else echo '{"value":"fake-token"}'; fi
""",
            "jq": "#!/bin/bash\ncat >/dev/null\nprintf fake-token\n",
            "az": """#!/bin/bash
echo "$*" >> "$MOCK_LOG"
if [[ "$*" == *'firewall-rule create'* && "${FAIL_CREATE:-0}" == 1 ]]; then exit 7; fi
if [[ "$*" == *'firewall-rule delete'* && "${FAIL_DELETE:-0}" == 1 ]]; then exit 8; fi
""",
            "sleep": "#!/bin/bash\nexit 0\n",
            "sqlcmd": """#!/bin/bash
[[ "$AZURE_CLIENT_ID" == client && "$AZURE_TENANT_ID" == tenant && -s "$AZURE_FEDERATED_TOKEN_FILE" ]] || exit 9
if [[ "${@: -1}" == 'SELECT 1' ]]; then
  n=$(cat "$MOCK_COUNT" 2>/dev/null || echo 0); n=$((n+1)); echo "$n" > "$MOCK_COUNT"
  if [[ "$MODE" == denied || ( "$MODE" == transient && "$n" -le 2 ) ]]; then
    echo "Client with IP address '8.8.8.8' is not allowed to access the server" >&2; exit 1
  fi
  if [[ "$MODE" == authfail ]]; then echo 'authentication failed' >&2; exit 4; fi
else
  echo "$*" >> "$MOCK_SQL_LOG"
fi
""",
        }
        for name, body in mocks.items():
            path = bin_dir / name
            path.write_text(body, encoding="utf-8")
            path.chmod(0o755)
        self.env = os.environ | {
            "PATH": str(bin_dir) + os.pathsep + os.environ["PATH"],
            "RUNNER_TEMP": str(self.root),
            "GITHUB_OUTPUT": str(self.root / "output"),
            "MOCK_LOG": str(self.root / "az-log"),
            "MOCK_COUNT": str(self.root / "count"),
            "MOCK_SQL_LOG": str(self.root / "sql-log"),
            "GITHUB_RUN_ID": "42",
            "GITHUB_RUN_ATTEMPT": "2",
            "TF_VAR_resource_group_name": "rg",
            "SQL_SERVER_NAME": "server",
            "SQL_DATABASE_NAME": "db",
            "APP_SERVICE_NAME": "app",
            "ARM_CLIENT_ID": "client",
            "ARM_TENANT_ID": "tenant",
            "ACTIONS_ID_TOKEN_REQUEST_TOKEN": "fake",
            "ACTIONS_ID_TOKEN_REQUEST_URL": "https://example.test?x=1",
            "MODE": "transient",
        }

    def run_step(self, name, **env):
        script = workflow_step(name).replace(
            "${{ steps.runner-firewall.outputs.rule_name }}", "github-run-42-2"
        )
        return subprocess.run(
            ["bash", "-e", "-o", "pipefail", "-c", script],
            env=self.env | env,
            text=True,
            capture_output=True,
        )

    def test_rule_is_narrow_and_cleanup_runs_on_failure(self):
        create = "Allow deployment runner to access Azure SQL"
        cleanup = "Remove deployment runner SQL firewall rule"
        self.assertEqual(self.run_step(create).returncode, 0)
        self.assertEqual((self.root / "output").read_text().strip(), "rule_name=github-run-42-2")
        self.assertIn("--start-ip-address 8.8.8.8 --end-ip-address 8.8.8.8", (self.root / "az-log").read_text())
        self.assertIn("if: ${{ always() && steps.runner-firewall.outputs.rule_name != '' }}", WORKFLOW.read_text())
        self.assertEqual(self.run_step(cleanup).returncode, 0)
        self.assertIn("firewall-rule delete", (self.root / "az-log").read_text())
        self.assertNotEqual(self.run_step(create, MOCK_IP="127.0.0.1").returncode, 0)
        self.assertEqual(self.run_step(create, FAIL_CREATE="1").returncode, 7)
        self.assertEqual(self.run_step(cleanup, FAIL_DELETE="1").returncode, 8)

    def test_retries_firewall_propagation_before_migration(self):
        result = self.run_step("Apply database schema and grant application access")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.root / "count").read_text().strip(), "3")
        self.assertEqual(len((self.root / "sql-log").read_text().splitlines()), 2)
        self.assertFalse(list(self.root.glob("sqlcmd-federated-token.*")))
        self.assertFalse(list(self.root.glob("sqlcmd-probe.*")))

    def test_stops_on_other_errors_and_bounds_firewall_retries(self):
        step = "Apply database schema and grant application access"
        result = self.run_step(step, MODE="authfail")
        self.assertEqual(result.returncode, 4)
        self.assertIn("authentication failed", result.stderr)
        self.assertEqual((self.root / "count").read_text().strip(), "1")
        (self.root / "count").unlink()
        result = self.run_step(step, MODE="denied")
        self.assertEqual(result.returncode, 1)
        self.assertEqual((self.root / "count").read_text().strip(), "31")
        self.assertFalse((self.root / "sql-log").exists())


if __name__ == "__main__":
    unittest.main()
