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
var applyMigrations = isE2e || builder.Configuration.GetValue("Database:ApplyMigrations", false);
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

if (applyMigrations && !string.IsNullOrWhiteSpace(connectionString))
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

app.MapGet(
        "/api/cards/{id:guid}/state-history",
        async (Guid id, IFleetCardService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetStateHistoryAsync(id, cancellationToken);
            return result.Error switch
            {
                CardCommandError.None => Results.Ok(result.History),
                CardCommandError.NotFound => Results.NotFound(),
                _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }
    )
    .RequireAuthorization()
    .WithName("GetCardStateHistory")
    .WithTags("Cards");

app.MapGet(
        "/api/transactions/report",
        async (
            HttpRequest request,
            IFleetCardsReadService service,
            ITransactionReportingService reportingService,
            CancellationToken cancellationToken
        ) =>
        {
            if (!TryGetTransactionReportRequest(request, out var reportRequest, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            TransactionReportPeriod period;
            try
            {
                period = reportingService.ResolvePeriod(reportRequest!);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [nameof(TransactionReportRequest.TimeZoneId)] = [exception.Message],
                    }
                );
            }

            return Results.Ok(await service.GetTransactionReportAsync(period, cancellationToken));
        }
    )
    .RequireAuthorization()
    .WithName("GetTransactionReport")
    .WithTags("Transactions");

app.MapGet(
        "/api/dashboard/summary",
        async (
            HttpRequest request,
            IFleetCardsReadService service,
            ITransactionReportingService reportingService,
            CancellationToken cancellationToken
        ) =>
        {
            if (!TryGetTransactionReportRequest(request, out var reportRequest, out var errors))
            {
                return Results.ValidationProblem(errors);
            }

            TransactionReportPeriod period;
            try
            {
                period = reportingService.ResolvePeriod(reportRequest!);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [nameof(TransactionReportRequest.TimeZoneId)] = [exception.Message],
                    }
                );
            }

            return Results.Ok(await service.GetDashboardSummaryAsync(period, cancellationToken));
        }
    )
    .RequireAuthorization()
    .WithName("GetDashboardSummary")
    .WithTags("Dashboard");

app.MapGet(
        "/api/programs",
        async (
            IFleetCardsReadService service,
            IProgramAssignmentService assignmentsService,
            CancellationToken cancellationToken
        ) =>
        {
            var programs = service.GetPrograms();
            var assignments = await assignmentsService.GetAssignmentsAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var currentAssignment = assignments.SingleOrDefault(assignment =>
                assignment.StartsAt <= now
                && (assignment.EndsAt is null || assignment.EndsAt > now)
            );
            return Results.Ok(new FleetCardProgramsResponse(programs, currentAssignment));
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("GetPrograms")
    .WithTags("Programs");

app.MapGet(
        "/api/programs/assignments",
        async (IProgramAssignmentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAssignmentsAsync(cancellationToken))
    )
    .RequireAuthorization("ReadWrite")
    .WithName("GetProgramAssignments")
    .WithTags("Programs");

app.MapPost(
        "/api/programs/assignments",
        async (
            ScheduleProgramAssignmentRequest request,
            HttpContext context,
            IProgramAssignmentService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (request.ProgramId is null || request.ProgramId == Guid.Empty)
            {
                errors[nameof(request.ProgramId)] = ["A valid programId is required."];
            }

            if (request.StartsAt is null)
            {
                errors[nameof(request.StartsAt)] = ["A valid startsAt timestamp is required."];
            }

            if (errors.Count > 0)
            {
                return (IResult)Results.ValidationProblem(errors);
            }

            var result = await service.ScheduleSwitchAsync(
                request.ProgramId!.Value,
                request.StartsAt!.Value,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            return MapProgramAssignmentCommandResult(result);
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("ScheduleProgramAssignment")
    .WithTags("Programs");

app.MapPost(
        "/api/programs/assignments/end",
        async (
            EndProgramAssignmentRequest request,
            HttpContext context,
            IProgramAssignmentService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (!CsrfProtection.IsValid(context))
            {
                return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.EndsAt is null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [nameof(request.EndsAt)] = ["A valid endsAt timestamp is required."],
                    }
                );
            }

            var result = await service.EndAsync(
                request.EndsAt.Value,
                context.User.Identity?.Name ?? "unknown",
                context.TraceIdentifier,
                cancellationToken
            );
            return MapProgramAssignmentCommandResult(result);
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("EndProgramAssignment")
    .WithTags("Programs");

app.MapGet(
        "/api/programs/comparison",
        async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            ProgramComparisonService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (from is null || to is null)
            {
                var errors = new Dictionary<string, string[]>();
                if (from is null)
                {
                    errors["from"] = ["A valid from timestamp is required."];
                }

                if (to is null)
                {
                    errors["to"] = ["A valid to timestamp is required."];
                }

                return Results.ValidationProblem(errors);
            }

            try
            {
                return Results.Ok(
                    await service.GetComparisonAsync(from.Value, to.Value, cancellationToken)
                );
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["from"] = [exception.Message] }
                );
            }
        }
    )
    .RequireAuthorization("ReadWrite")
    .WithName("GetProgramComparison")
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

static IResult MapProgramAssignmentCommandResult(ProgramAssignmentCommandResult result) =>
    result.Error switch
    {
        ProgramAssignmentCommandError.None => Results.NoContent(),
        ProgramAssignmentCommandError.Validation => Results.ValidationProblem(
            result.ValidationErrors!.ToDictionary(error => error.Key, error => error.Value)
        ),
        ProgramAssignmentCommandError.NotFound => Results.NotFound(),
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
        vehiclePlates.TryGetValue(card.AssignedCarId, out var plate) ? plate : null
    );

static bool TryGetTransactionReportRequest(
    HttpRequest request,
    out TransactionReportRequest? reportRequest,
    out Dictionary<string, string[]> errors
)
{
    errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    var year = ParseRequiredInteger(request, "year", nameof(TransactionReportRequest.Year), errors);
    var month = ParseRequiredInteger(
        request,
        "month",
        nameof(TransactionReportRequest.Month),
        errors
    );
    var timeZoneValues = request.Query["timeZone"];
    if (timeZoneValues.Count != 1 || string.IsNullOrWhiteSpace(timeZoneValues[0]))
    {
        errors[nameof(TransactionReportRequest.TimeZoneId)] = ["A timezone is required."];
    }

    if (year is < 1 or > 9998)
    {
        errors[nameof(TransactionReportRequest.Year)] = ["Year must be between 1 and 9998."];
    }

    if (month is < 1 or > 12)
    {
        errors[nameof(TransactionReportRequest.Month)] = ["Month must be between 1 and 12."];
    }

    reportRequest =
        errors.Count == 0
            ? new TransactionReportRequest(year!.Value, month!.Value, timeZoneValues[0])
            : null;
    return errors.Count == 0;
}

static int? ParseRequiredInteger(
    HttpRequest request,
    string queryName,
    string errorKey,
    IDictionary<string, string[]> errors
)
{
    var values = request.Query[queryName];
    if (
        values.Count == 1
        && int.TryParse(
            values[0],
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value
        )
    )
    {
        return value;
    }

    errors[errorKey] = [$"A valid {queryName} is required."];
    return null;
}

public partial class Program;

internal sealed record ApiInfo(string Name, string Version);
