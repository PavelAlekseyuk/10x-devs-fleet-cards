using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Infrastructure;

public sealed class EfFleetVehicleService(FleetCardsDbContext dbContext) : IFleetVehicleService
{
    public async Task<IReadOnlyList<FleetCarResponse>> GetCarsAsync(
        string? search,
        bool includeArchived = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<FleetCar> query = dbContext.Cars.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(car => !car.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = FleetVehicleRules.NormalizeRegistrationPlate(search);
            if (searchTerm.Length > 0)
            {
                query = query.Where(car => car.NormalizedRegistrationPlate.Contains(searchTerm));
            }
        }

        return await query
            .OrderBy(car => car.RegistrationPlate)
            .Select(car => new FleetCarResponse(
                car.Id,
                car.RegistrationPlate,
                car.FuelType,
                car.Make,
                car.Model,
                car.IsArchived
            ))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<VehicleCommandResult> CreateAsync(
        CreateFleetCarRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var validation = FleetVehicleRules.Validate(
            request.RegistrationPlate,
            request.FuelType,
            request.Make,
            request.Model
        );
        if (validation is not null)
        {
            return validation;
        }

        var displayPlate = request.RegistrationPlate.Trim();
        var normalizedPlate = FleetVehicleRules.NormalizeRegistrationPlate(displayPlate);
        var clientId = await dbContext.Clients
            .Select(client => client.Id)
            .SingleAsync(cancellationToken);
        if (await HasActivePlateAsync(clientId, normalizedPlate, null, cancellationToken))
        {
            return VehicleCommandResult.Conflict();
        }

        var car = new FleetCar
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            RegistrationPlate = displayPlate,
            NormalizedRegistrationPlate = normalizedPlate,
            FuelType = request.FuelType,
            Make = FleetVehicleRules.NormalizeDescription(request.Make),
            Model = FleetVehicleRules.NormalizeDescription(request.Model),
        };
        dbContext.Cars.Add(car);
        return await SaveSuccessfulChangeAsync(
            car,
            "VehicleCreated",
            actor,
            correlationId,
            cancellationToken
        );
    }

    public async Task<VehicleCommandResult> UpdateAsync(
        Guid id,
        UpdateFleetCarRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var validation = FleetVehicleRules.Validate(
            request.RegistrationPlate,
            request.FuelType,
            request.Make,
            request.Model
        );
        if (validation is not null)
        {
            return validation;
        }

        var car = await dbContext.Cars.SingleOrDefaultAsync(vehicle => vehicle.Id == id, cancellationToken);
        if (car is null)
        {
            return VehicleCommandResult.NotFound();
        }

        var displayPlate = request.RegistrationPlate.Trim();
        var normalizedPlate = FleetVehicleRules.NormalizeRegistrationPlate(displayPlate);
        if (
            !car.IsArchived
            && await HasActivePlateAsync(car.ClientId, normalizedPlate, id, cancellationToken)
        )
        {
            return VehicleCommandResult.Conflict();
        }

        car.RegistrationPlate = displayPlate;
        car.NormalizedRegistrationPlate = normalizedPlate;
        car.FuelType = request.FuelType;
        car.Make = FleetVehicleRules.NormalizeDescription(request.Make);
        car.Model = FleetVehicleRules.NormalizeDescription(request.Model);
        return await SaveSuccessfulChangeAsync(
            car,
            "VehicleUpdated",
            actor,
            correlationId,
            cancellationToken
        );
    }

    public async Task<VehicleCommandResult> ArchiveAsync(
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var car = await dbContext.Cars.SingleOrDefaultAsync(vehicle => vehicle.Id == id, cancellationToken);
        if (car is null)
        {
            return VehicleCommandResult.NotFound();
        }

        car.IsArchived = true;
        return await SaveSuccessfulChangeAsync(
            car,
            "VehicleArchived",
            actor,
            correlationId,
            cancellationToken
        );
    }

    public async Task<VehicleCommandResult> RestoreAsync(
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var car = await dbContext.Cars.SingleOrDefaultAsync(vehicle => vehicle.Id == id, cancellationToken);
        if (car is null)
        {
            return VehicleCommandResult.NotFound();
        }

        if (
            await HasActivePlateAsync(
                car.ClientId,
                car.NormalizedRegistrationPlate,
                id,
                cancellationToken
            )
        )
        {
            return VehicleCommandResult.Conflict();
        }

        car.IsArchived = false;
        return await SaveSuccessfulChangeAsync(
            car,
            "VehicleRestored",
            actor,
            correlationId,
            cancellationToken
        );
    }

    public Task<bool> IsEligibleForAssignmentAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => dbContext.Cars.AnyAsync(car => car.Id == id && !car.IsArchived, cancellationToken);

    private Task<bool> HasActivePlateAsync(
        Guid clientId,
        string normalizedPlate,
        Guid? exceptId,
        CancellationToken cancellationToken
    )
    {
        IQueryable<FleetCar> query = dbContext.Cars.Where(car =>
            car.ClientId == clientId
            && !car.IsArchived
            && car.NormalizedRegistrationPlate == normalizedPlate
        );
        if (exceptId is not null)
        {
            query = query.Where(car => car.Id != exceptId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    private async Task<VehicleCommandResult> SaveSuccessfulChangeAsync(
        FleetCar car,
        string action,
        string actor,
        string correlationId,
        CancellationToken cancellationToken
    )
    {
        dbContext.AuditEvents.Add(
            new FleetAuditEvent
            {
                Id = Guid.NewGuid(),
                Actor = actor,
                Action = action,
                EntityType = "FleetCar",
                EntityId = car.Id,
                OccurredAt = DateTimeOffset.UtcNow,
                Succeeded = true,
                CorrelationId = correlationId,
            }
        );

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActivePlateUniqueViolation(exception))
        {
            return VehicleCommandResult.Conflict();
        }

        return VehicleCommandResult.Success(ToResponse(car));
    }

    private static bool IsActivePlateUniqueViolation(DbUpdateException exception)
    {
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (
                cause is SqlException sqlException
                && sqlException.Number is 2601 or 2627
                && sqlException.Message.Contains(
                    "IX_Cars_ClientId_NormalizedRegistrationPlate",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static FleetCarResponse ToResponse(FleetCar car) =>
        new(car.Id, car.RegistrationPlate, car.FuelType, car.Make, car.Model, car.IsArchived);
}