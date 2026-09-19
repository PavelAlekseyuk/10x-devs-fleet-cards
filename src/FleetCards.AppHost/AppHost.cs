var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.FleetCards_Api>("api");
builder.AddProject<Projects.FleetCards_Worker>("worker")
    .WithReference(api);

builder.AddExecutable("web", "npm", workingDirectory: "../../web")
    .WithArgs("start", "--", "--host", "0.0.0.0", "--port", "4200")
    .WithReference(api);

builder.Build().Run();
