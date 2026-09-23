using FleetCards.Contracts;
using FleetCards.Domain;

namespace FleetCards.Application;

public sealed class SeededFleetVehicleService : IFleetVehicleService
{
    private static readonly Guid ClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly object sync = new();
    private readonly List<FleetCar> cars =
    [
        new FleetCar
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ClientId = ClientId,
            RegistrationPlate = "WA 12345",
            NormalizedRegistrationPlate = "WA12345",
            FuelType = "Diesel",
            Make = "Volvo",
            Model = "FH",
        },
        new FleetCar
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ClientId = ClientId,
            RegistrationPlate = "PO 98765",
            NormalizedRegistrationPlate = "PO98765",
            FuelType = "95",
            Make = "Toyota",
            Model = "Corolla",
        },
    ];
    private readonly List<FleetAuditEvent> auditEvents = [];

    public Task<IReadOnlyList<FleetCarResponse>> GetCarsAsync(
        string? search,
        bool includeArchived = false,
        CancellationToken cancellationToken = default
    )
    {
        var searchTerm = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : FleetVehicleRules.NormalizeRegistrationPlate(search);
        lock (sync)
        {
            IReadOnlyList<FleetCarResponse> result = cars
                .Where(car => includeArchived || !car.IsArchived)
                .Where(car => searchTerm.Length == 0 || car.NormalizedRegistrationPlate.Contains(searchTerm))
                .OrderBy(car => car.RegistrationPlate, StringComparer.OrdinalIgnoreCase)
                .Select(ToResponse)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<VehicleCommandResult> CreateAsync(
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
            return Task.FromResult(validation);
        }

        lock (sync)
        {
            var normalizedPlate = FleetVehicleRules.NormalizeRegistrationPlate(
                request.RegistrationPlate
            );
            if (HasActivePlate(normalizedPlate))
            {
                return Task.FromResult(VehicleCommandResult.Conflict());
            }

            var car = new FleetCar
            {
                Id = Guid.NewGuid(),
                ClientId = ClientId,
                RegistrationPlate = request.RegistrationPlate.Trim(),
                NormalizedRegistrationPlate = normalizedPlate,
                FuelType = request.FuelType,
                Make = FleetVehicleRules.NormalizeDescription(request.Make),
                Model = FleetVehicleRules.NormalizeDescription(request.Model),
            };
            cars.Add(car);
            AddAudit(car.Id, "VehicleCreated", actor, correlationId);
            return Task.FromResult(VehicleCommandResult.Success(ToResponse(car)));
        }
    }

    public Task<VehicleCommandResult> UpdateAsync(
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
            return Task.FromResult(validation);
        }

        lock (sync)
        {
            var car = cars.SingleOrDefault(vehicle => vehicle.Id == id);
            if (car is null)
            {
                return Task.FromResult(VehicleCommandResult.NotFound());
            }

            var normalizedPlate = FleetVehicleRules.NormalizeRegistrationPlate(
                request.RegistrationPlate
            );
            if (!car.IsArchived && HasActivePlate(normalizedPlate, id))
            {
                return Task.FromResult(VehicleCommandResult.Conflict());
            }

            car.RegistrationPlate = request.RegistrationPlate.Trim();
            car.NormalizedRegistrationPlate = normalizedPlate;
            car.FuelType = request.FuelType;
            car.Make = FleetVehicleRules.NormalizeDescription(request.Make);
            car.Model = FleetVehicleRules.NormalizeDescription(request.Model);
            AddAudit(car.Id, "VehicleUpdated", actor, correlationId);
            return Task.FromResult(VehicleCommandResult.Success(ToResponse(car)));
        }
    }

    public Task<VehicleCommandResult> ArchiveAsync(
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        lock (sync)
        {
            var car = cars.SingleOrDefault(vehicle => vehicle.Id == id);
            if (car is null)
            {
                return Task.FromResult(VehicleCommandResult.NotFound());
            }

            car.IsArchived = true;
            AddAudit(car.Id, "VehicleArchived", actor, correlationId);
            return Task.FromResult(VehicleCommandResult.Success(ToResponse(car)));
        }
    }

    public Task<VehicleCommandResult> RestoreAsync(
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        lock (sync)
        {
            var car = cars.SingleOrDefault(vehicle => vehicle.Id == id);
            if (car is null)
            {
                return Task.FromResult(VehicleCommandResult.NotFound());
            }

            if (HasActivePlate(car.NormalizedRegistrationPlate, id))
            {
                return Task.FromResult(VehicleCommandResult.Conflict());
            }

            car.IsArchived = false;
            AddAudit(car.Id, "VehicleRestored", actor, correlationId);
            return Task.FromResult(VehicleCommandResult.Success(ToResponse(car)));
        }
    }

    public Task<bool> IsEligibleForAssignmentAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        lock (sync)
        {
            return Task.FromResult(cars.Any(car => car.Id == id && !car.IsArchived));
        }
    }

    private bool HasActivePlate(string normalizedPlate, Guid? exceptId = null) =>
        cars.Any(car =>
            !car.IsArchived
            && car.Id != exceptId
            && string.Equals(
                car.NormalizedRegistrationPlate,
                normalizedPlate,
                StringComparison.Ordinal
            )
        );

    private void AddAudit(Guid entityId, string action, string actor, string correlationId) =>
        auditEvents.Add(
            new FleetAuditEvent
            {
                Id = Guid.NewGuid(),
                Actor = actor,
                Action = action,
                EntityType = "FleetCar",
                EntityId = entityId,
                OccurredAt = DateTimeOffset.UtcNow,
                Succeeded = true,
                CorrelationId = correlationId,
            }
        );

    private static FleetCarResponse ToResponse(FleetCar car) =>
        new(car.Id, car.RegistrationPlate, car.FuelType, car.Make, car.Model, car.IsArchived);
}