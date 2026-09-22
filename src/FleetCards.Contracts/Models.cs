namespace FleetCards.Contracts;

public sealed record LoginRequest(string UserName, string Password);

public sealed record CurrentUserResponse(string UserName, string Permission);

public sealed record ClientResponse(
    string LegalName,
    string Nip,
    string Jurisdiction,
    string Address
);

public sealed record FleetCarResponse(
    Guid Id,
    string RegistrationPlate,
    string? Make,
    string? Model,
    bool IsArchived
);

public sealed record FleetCardResponse(
    Guid Id,
    string MaskedPan,
    string ProviderToken,
    string State,
    Guid? AssignedCarId
);

public sealed record FuelTransactionResponse(
    Guid Id,
    Guid CardId,
    Guid? VehicleId,
    string VehiclePlate,
    DateTimeOffset OccurredAt,
    string StationName,
    string FuelType,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal NetTotal,
    decimal VatRate,
    decimal VatAmount,
    decimal GrossTotal,
    string Status
);

public sealed record FleetCardProgramResponse(
    Guid Id,
    string Name,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveTo
);
