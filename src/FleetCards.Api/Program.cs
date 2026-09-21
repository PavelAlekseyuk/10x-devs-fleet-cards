using FleetCards.Application;
using FleetCards.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddFleetCardsInfrastructure();

var app = builder.Build();
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api", () => new ApiInfo("Fleet Cards API", "0.1.0"))
    .WithName("GetApiInfo")
    .WithTags("Platform");

app.MapGet("/api/auth/me", (IFleetCardsReadService service) => service.GetCurrentUser())
    .WithName("GetCurrentUser")
    .WithTags("Auth");

app.MapGet("/api/client", (IFleetCardsReadService service) => service.GetClient())
    .WithName("GetClient")
    .WithTags("Client");

app.MapGet("/api/cars", (string? search, IFleetCardsReadService service) => service.GetCars(search))
    .WithName("GetCars")
    .WithTags("Cars");

app.MapGet("/api/cards", (IFleetCardsReadService service) => service.GetCards())
    .WithName("GetCards")
    .WithTags("Cards");

app.MapGet(
        "/api/transactions",
        (DateTimeOffset? from, DateTimeOffset? to, IFleetCardsReadService service) =>
            service.GetTransactions(from, to)
    )
    .WithName("GetTransactions")
    .WithTags("Transactions");

app.MapGet("/api/programs", (IFleetCardsReadService service) => service.GetPrograms())
    .WithName("GetPrograms")
    .WithTags("Programs");

app.MapDefaultEndpoints();
app.MapFallback(async context =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (
        path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || Path.HasExtension(path)
    )
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await Results
        .File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html")
        .ExecuteAsync(context);
});
app.Run();

public partial class Program;

internal sealed record ApiInfo(string Name, string Version);
