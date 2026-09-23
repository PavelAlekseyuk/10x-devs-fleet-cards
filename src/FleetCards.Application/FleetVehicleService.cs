using FleetCards.Contracts;

namespace FleetCards.Application;

public enum VehicleCommandError
{
    None,
    Validation,
    NotFound,
    Conflict,
}

public sealed record VehicleCommandResult(
    FleetCarResponse? Vehicle,
    VehicleCommandError Error,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null
)
{
    public bool IsSuccess => Error == VehicleCommandError.None;

    public static VehicleCommandResult Success(FleetCarResponse vehicle) =>
        new(vehicle, VehicleCommandError.None);

    public static VehicleCommandResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, VehicleCommandError.Validation, errors);

    public static VehicleCommandResult NotFound() => new(null, VehicleCommandError.NotFound);

    public static VehicleCommandResult Conflict() => new(null, VehicleCommandError.Conflict);
}

public interface IFleetVehicleService
{
    Task<IReadOnlyList<FleetCarResponse>> GetCarsAsync(
        string? search,
        bool includeArchived = false,
        CancellationToken cancellationToken = default
    );

    Task<VehicleCommandResult> CreateAsync(
        CreateFleetCarRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<VehicleCommandResult> UpdateAsync(
        Guid id,
        UpdateFleetCarRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<VehicleCommandResult> ArchiveAsync(
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<VehicleCommandResult> RestoreAsync(
        Guid id,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<bool> IsEligibleForAssignmentAsync(Guid id, CancellationToken cancellationToken = default);
}

public static class FleetVehicleRules
{
    private static readonly HashSet<string> SupportedFuelTypes = new(StringComparer.Ordinal)
    {
        "95",
        "98",
        "Diesel",
        "95+",
        "98+",
        "Diesel+",
    };

    public static string NormalizeRegistrationPlate(string registrationPlate)
    {
        var trimmedPlate = registrationPlate.Trim().ToUpperInvariant();
        return string.Concat(trimmedPlate.Where(character => !char.IsWhiteSpace(character)));
    }

    public static VehicleCommandResult? Validate(
        string? registrationPlate,
        string? fuelType,
        string? make,
        string? model
    )
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(registrationPlate))
        {
            errors[nameof(CreateFleetCarRequest.RegistrationPlate)] =
            [
                "Registration plate is required.",
            ];
        }
        else if (registrationPlate.Trim().Length > 20)
        {
            errors[nameof(CreateFleetCarRequest.RegistrationPlate)] =
            [
                "Registration plate must be 20 characters or fewer.",
            ];
        }

        if (fuelType is null || !SupportedFuelTypes.Contains(fuelType))
        {
            errors[nameof(CreateFleetCarRequest.FuelType)] =
            [
                "Fuel type must be one of the supported values.",
            ];
        }

        if (make?.Trim().Length > 100)
        {
            errors[nameof(CreateFleetCarRequest.Make)] = ["Make must be 100 characters or fewer."];
        }

        if (model?.Trim().Length > 100)
        {
            errors[nameof(CreateFleetCarRequest.Model)] =
            [
                "Model must be 100 characters or fewer.",
            ];
        }

        return errors.Count == 0 ? null : VehicleCommandResult.Invalid(errors);
    }

    public static string? NormalizeDescription(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
