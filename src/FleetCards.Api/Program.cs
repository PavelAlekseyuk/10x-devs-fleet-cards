using System.Security.Claims;
using System.Text.RegularExpressions;
using FleetCards.Api;
using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var e2eDatabaseName = builder.Configuration["FLEETCARDS_E2E_DATABASE"];
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

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "FleetCards.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadWrite", policy => policy.RequireClaim("permission", "ReadWrite"));
});
builder.Services.AddSingleton<LoginAttemptTracker>();
builder.Services.AddFleetCardsInfrastructure(builder.Configuration);

var app = builder.Build();

var connectionString = builder.Configuration.GetConnectionString("fleetcards");
if (isE2e && string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("E2E mode requires the fleetcards database connection.");
}

if (!string.IsNullOrWhiteSpace(connectionString))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
    if (isE2e)
    {
        var actualDatabaseName = dbContext.Database.GetDbConnection().Database;
        if (
            !Regex.IsMatch(
                actualDatabaseName,
                @"\Afleetcards_e2e_[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant
            ) || !string.Equals(actualDatabaseName, e2eDatabaseName, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "The configured database does not match FLEETCARDS_E2E_DATABASE."
            );
        }
    }

    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler(
    new Microsoft.AspNetCore.Builder.ExceptionHandlerOptions
    {
        StatusCodeSelector = exception =>
            exception is BadHttpRequestException
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status500InternalServerError,
    }
);
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.Use(
    async (context, next) =>
    {
        await next();
        if (
            context.Response.StatusCode == StatusCodes.Status403Forbidden
            && context.Request.Path.StartsWithSegments("/api")
        )
        {
            app.Logger.LogWarning(
                "Authorization denied for {Path} with correlation {CorrelationId}",
                context.Request.Path,
                context.TraceIdentifier
            );
        }
    }
);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api", () => new ApiInfo("Fleet Cards API", "0.1.0"))
    .WithName("GetApiInfo")
    .WithTags("Platform");

if (isE2e)
{
    app.MapGet(
            "/api/e2e/database",
            (FleetCardsDbContext dbContext) =>
                Results.Ok(new { databaseName = dbContext.Database.GetDbConnection().Database })
        )
        .RequireAuthorization()
        .WithName("GetE2EDatabaseIdentity")
        .WithTags("Platform");
}

app.MapGet(
        "/api/auth/csrf",
        (HttpContext context) =>
        {
            CsrfProtection.Issue(context);
            return Results.NoContent();
        }
    )
    .AllowAnonymous()
    .WithName("GetCsrfToken")
    .WithTags("Auth");

app.MapPost(
        "/api/auth/login",
        async (
            LoginRequest request,
            HttpContext context,
            FleetCards.Application.IAuthenticationService authentication,
            LoginAttemptTracker throttle
        ) =>
        {
            var correlationId = context.TraceIdentifier;
            if (!CsrfProtection.IsValid(context))
            {
                app.Logger.LogWarning(
                    "Authentication rejected for {Reason} with correlation {CorrelationId}",
                    "csrf",
                    correlationId
                );
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var userName = request.UserName.Trim();
            var throttleKey = $"{context.Connection.RemoteIpAddress}|{userName}";
            if (throttle.IsThrottled(throttleKey))
            {
                app.Logger.LogWarning(
                    "Authentication throttled for {UserName} with correlation {CorrelationId}",
                    userName,
                    correlationId
                );
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            var user = authentication.ValidateCredentials(userName, request.Password);
            if (user is null)
            {
                throttle.RecordFailure(throttleKey);
                app.Logger.LogWarning(
                    "Authentication failed for {UserName} with correlation {CorrelationId}",
                    userName,
                    correlationId
                );
                return Results.Unauthorized();
            }

            throttle.Reset(throttleKey);
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("permission", user.Permission),
            };
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(
                    new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
                )
            );
            app.Logger.LogInformation(
                "Authentication succeeded for {UserName} with {Permission} and correlation {CorrelationId}",
                user.UserName,
                user.Permission,
                correlationId
            );
            return Results.Ok(user);
        }
    )
    .AllowAnonymous()
    .WithName("Login")
    .WithTags("Auth");

app.MapPost(
        "/api/auth/logout",
        async (HttpContext context) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var userName = context.User.Identity?.Name ?? "unknown";
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Cookies.Delete(CsrfProtection.CookieName);
            app.Logger.LogInformation(
                "Authentication logout for {UserName} with correlation {CorrelationId}",
                userName,
                context.TraceIdentifier
            );
            return Results.NoContent();
        }
    )
    .RequireAuthorization()
    .WithName("Logout")
    .WithTags("Auth");

app.MapGet(
        "/api/auth/me",
        (HttpContext context, FleetCards.Application.IAuthenticationService authentication) =>
        {
            var userName = context.User.Identity?.Name;
            var user = userName is null ? null : authentication.GetCurrentUser(userName);
            return user is null ? Results.Unauthorized() : Results.Ok(user);
        }
    )
    .RequireAuthorization()
    .WithName("GetCurrentUser")
    .WithTags("Auth");

app.MapGet("/api/client", (IFleetCardsReadService service) => service.GetClient())
    .RequireAuthorization()
    .WithName("GetClient")
    .WithTags("Client");

app.MapGet(
        "/api/cars",
        async (
            string? search,
            IFleetVehicleService service,
            bool includeArchived = false,
            CancellationToken cancellationToken = default
        ) => await service.GetCarsAsync(search, includeArchived, cancellationToken)
    )
    .RequireAuthorization()
    .WithName("GetCars")
    .WithTags("Cars");

app.MapPost(
        "/api/cars",
        async (
            CreateFleetCarRequest request,
            HttpContext context,
            IFleetVehicleService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await service.CreateAsync(
                request,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            if (!result.IsSuccess)
            {
                return MapVehicleCommandResult(result);
            }

            return (IResult)Results.Created($"/api/cars/{result.Vehicle!.Id}", result.Vehicle);
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("CreateCar")
    .WithTags("Cars");

app.MapPut(
        "/api/cars/{id:guid}",
        async (
            Guid id,
            UpdateFleetCarRequest request,
            HttpContext context,
            IFleetVehicleService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await service.UpdateAsync(
                id,
                request,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            return MapVehicleCommandResult(result);
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("UpdateCar")
    .WithTags("Cars");

app.MapPost(
        "/api/cars/{id:guid}/archive",
        async (
            Guid id,
            HttpContext context,
            IFleetVehicleService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await service.ArchiveAsync(
                id,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            return MapVehicleCommandResult(result);
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("ArchiveCar")
    .WithTags("Cars");

app.MapPost(
        "/api/cars/{id:guid}/restore",
        async (
            Guid id,
            HttpContext context,
            IFleetVehicleService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await service.RestoreAsync(
                id,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            return MapVehicleCommandResult(result);
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("RestoreCar")
    .WithTags("Cars");

app.MapGet(
        "/api/cards",
        async (
            IFleetCardService service,
            IFleetVehicleService vehicleService,
            CancellationToken cancellationToken
        ) =>
        {
            var cards = service.GetCards();
            var vehiclePlates = await GetVehiclePlatesAsync(vehicleService, cancellationToken);
            return cards.Select(card => ToCardApiResponse(card, vehiclePlates)).ToArray();
        }
    )
    .RequireAuthorization()
    .WithName("GetCards")
    .WithTags("Cards");

app.MapPost(
        "/api/cards",
        async (
            IssueFleetCardRequest request,
            HttpContext context,
            IFleetCardService service,
            IFleetVehicleService vehicleService,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await service.IssueAsync(
                request,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            if (!result.IsSuccess)
            {
                return MapCardCommandResult(result);
            }

            var vehiclePlates = await GetVehiclePlatesAsync(vehicleService, cancellationToken);
            var card = ToCardApiResponse(result.Card!, vehiclePlates);
            return (IResult)Results.Created($"/api/cards/{card.Id}", card);
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("IssueCard")
    .WithTags("Cards");

app.MapPut(
        "/api/cards/{id:guid}/state",
        async (
            Guid id,
            ChangeFleetCardStateRequest request,
            HttpContext context,
            IFleetCardService service,
            IFleetVehicleService vehicleService,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await service.ChangeStateAsync(
                id,
                request,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            if (!result.IsSuccess)
            {
                return MapCardCommandResult(result);
            }

            var vehiclePlates = await GetVehiclePlatesAsync(vehicleService, cancellationToken);
            return (IResult)Results.Ok(ToCardApiResponse(result.Card!, vehiclePlates));
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("ChangeCardState")
    .WithTags("Cards");

app.MapPut(
        "/api/cards/{id:guid}/assignment",
        async (
            Guid id,
            ReassignFleetCardRequest request,
            HttpContext context,
            IFleetCardService service,
            IFleetVehicleService vehicleService,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await service.ReassignAsync(
                id,
                request,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            if (!result.IsSuccess)
            {
                return MapCardCommandResult(result);
            }

            var vehiclePlates = await GetVehiclePlatesAsync(vehicleService, cancellationToken);
            return (IResult)Results.Ok(ToCardApiResponse(result.Card!, vehiclePlates));
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("ReassignCard")
    .WithTags("Cards");

app.MapGet(
        "/api/cards/{id:guid}/assignment-history",
        async (Guid id, IFleetCardService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAssignmentHistoryAsync(id, cancellationToken);
            return result.Error switch
            {
                CardCommandError.None => Results.Ok(result.Assignments),
                CardCommandError.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }
    )
    .RequireAuthorization()
    .WithName("GetCardAssignmentHistory")
    .WithTags("Cards");

app.MapGet(
        "/api/transactions",
        (DateTimeOffset? from, DateTimeOffset? to, IFleetCardsReadService service) =>
            service.GetTransactions(from, to)
    )
    .RequireAuthorization()
    .WithName("GetTransactions")
    .WithTags("Transactions");

app.MapGet("/api/programs", (IFleetCardsReadService service) => service.GetPrograms())
    .RequireAuthorization("ReadWrite")
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

static IResult MapVehicleCommandResult(VehicleCommandResult result) =>
    result.Error switch
    {
        VehicleCommandError.None => Results.Ok(result.Vehicle),
        VehicleCommandError.Validation => Results.ValidationProblem(
            result.ValidationErrors!.ToDictionary(error => error.Key, error => error.Value)
        ),
        VehicleCommandError.NotFound => Results.NotFound(),
        VehicleCommandError.Conflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Vehicle registration plate conflict",
            detail: "An active vehicle already uses this registration plate."
        ),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

static IResult MapCardCommandResult(CardCommandResult result) =>
    result.Error switch
    {
        CardCommandError.Validation => Results.ValidationProblem(
            result.ValidationErrors!.ToDictionary(error => error.Key, error => error.Value)
        ),
        CardCommandError.NotFound => Results.NotFound(),
        CardCommandError.Conflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Card lifecycle conflict",
            detail: "The requested operation is not valid for the card or vehicle state."
        ),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

static async Task<IReadOnlyDictionary<Guid, string>> GetVehiclePlatesAsync(
    IFleetVehicleService vehicleService,
    CancellationToken cancellationToken
)
{
    var vehicles = await vehicleService.GetCarsAsync(
        search: null,
        includeArchived: true,
        cancellationToken
    );
    return vehicles.ToDictionary(vehicle => vehicle.Id, vehicle => vehicle.RegistrationPlate);
}

static FleetCardApiResponse ToCardApiResponse(
    FleetCardResponse card,
    IReadOnlyDictionary<Guid, string> vehiclePlates
) =>
    new(
        card.Id,
        card.MaskedPan,
        card.ProviderToken,
        card.State,
        card.AssignedCarId,
        card.AssignedCarId is Guid vehicleId && vehiclePlates.TryGetValue(vehicleId, out var plate)
            ? plate
            : null
    );

public partial class Program;

internal sealed record ApiInfo(string Name, string Version);
