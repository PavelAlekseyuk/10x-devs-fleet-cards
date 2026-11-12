'use strict';

const RETENTION_MS = 30 * 24 * 60 * 60 * 1000;
const KEEP_PER_WORKFLOW = 10;

function selectRunsForDeletion(runs, now) {
  if (!Number.isFinite(now)) {
    throw new Error('A valid current time is required.');
  }

  const sorted = runs.map((run) => {
    const created = Date.parse(run.created_at);
    if (!Number.isSafeInteger(run.id) || !Number.isSafeInteger(run.workflow_id) ||
        !Number.isFinite(created) || typeof run.status !== 'string') {
      throw new Error('Workflow run is missing a valid id, workflow id, creation time, or status.');
    }
    return { run, created };
  }).sort((a, b) => b.created - a.created || b.run.id - a.run.id);

  const seen = new Map();
  const selected = [];
  for (const { run, created } of sorted) {
    const count = (seen.get(run.workflow_id) ?? 0) + 1;
    seen.set(run.workflow_id, count);
    if (count > KEEP_PER_WORKFLOW && run.status === 'completed' && created < now - RETENTION_MS) {
      selected.push(run);
    }
  }
  return selected;
}

module.exports = { selectRunsForDeletion };
