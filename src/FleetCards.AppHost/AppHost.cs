var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .AddDatabase("fleetcards");

var api = builder.AddProject<Projects.FleetCards_Api>("api");
api.WithReference(sql);

builder.AddExecutable("web", "npm", workingDirectory: "../../web")
    .WithArgs("start", "--", "--host", "0.0.0.0", "--port", "4200")
    .WithReference(api);

builder.Build().Run();
