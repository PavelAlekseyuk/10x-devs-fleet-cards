var builder = DistributedApplication.CreateBuilder(args);

var sql = builder
    .AddSqlServer("sql")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("fleetcards");

var api = builder.AddProject<Projects.FleetCards_Api>("api");
api.WithReference(sql);

builder
    .AddExecutable("web", "npm", workingDirectory: "../../web")
    .WithArgs(
        "start",
        "--",
        "--host",
        "0.0.0.0",
        "--port",
        "4200",
        "--proxy-config",
        "proxy.conf.json"
    )
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithReference(api);

await builder.Build().RunAsync();
