using System.Text.RegularExpressions;

var builder = DistributedApplication.CreateBuilder(args);

var e2eDatabaseName = Environment.GetEnvironmentVariable("FLEETCARDS_E2E_DATABASE");
var isE2e = e2eDatabaseName is not null;
if (
    isE2e
    && !Regex.IsMatch(
        e2eDatabaseName!,
        @"\Afleetcards_e2e_[0-9a-f]{32}\z",
        RegexOptions.CultureInvariant
    )
)
{
    throw new InvalidOperationException("FLEETCARDS_E2E_DATABASE has an invalid value.");
}

var sqlServer = builder.AddSqlServer("sql");
var sql = isE2e
    ? sqlServer.WithLifetime(ContainerLifetime.Session).AddDatabase("fleetcards", e2eDatabaseName!)
    : sqlServer
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent)
        .AddDatabase("fleetcards");

var api = builder.AddProject<Projects.FleetCards_Api>("api");
api.WithReference(sql);
api.WaitFor(sql);
if (isE2e)
{
    api.WithEnvironment("FLEETCARDS_E2E_DATABASE", e2eDatabaseName!);
}

var web = builder.AddExecutable("web", "npm", workingDirectory: "../../web");
web.WithArgs(
        "start",
        "--",
        "--host",
        "0.0.0.0",
        "--port",
        "4200",
        "--proxy-config",
        isE2e ? "proxy.e2e.conf.cjs" : "proxy.conf.json"
    )
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithReference(api);
if (isE2e)
{
    web.WithEnvironment("FLEETCARDS_API_URL", api.GetEndpoint("http"));
}

await builder.Build().RunAsync();
