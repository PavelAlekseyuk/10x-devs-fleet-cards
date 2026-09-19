var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => new ApiInfo("Fleet Cards API", "0.1.0"))
    .WithName("GetApiInfo")
    .WithTags("Platform");

app.MapDefaultEndpoints();
app.Run();

public partial class Program;

internal sealed record ApiInfo(string Name, string Version);
