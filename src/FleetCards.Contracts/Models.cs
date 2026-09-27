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
    string FuelType,
    string? Make,
    string? Model,
    bool IsArchived
);

public sealed record CreateFleetCarRequest(
    string RegistrationPlate,
    string FuelType,
    string? Make,
    string? Model
);

public sealed record UpdateFleetCarRequest(
    string RegistrationPlate,
    string FuelType,
    string? Make,
    string? Model
);

public sealed record FleetCardResponse(
    Guid Id,
    string MaskedPan,
    string ProviderToken,
    string State,
    Guid AssignedCarId
);

public sealed record FleetCardApiResponse(
    Guid Id,
    string MaskedPan,
    string ProviderToken,
    string State,
    Guid AssignedCarId,
    string? AssignedVehicleRegistrationPlate
);

public sealed record IssueFleetCardRequest(Guid VehicleId);

public sealed record ChangeFleetCardStateRequest(string State);

public sealed record FleetCardStateHistoryResponse(string State, DateTimeOffset OccurredAt);

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

public sealed record TransactionReportRequest(int Year, int Month, string? TimeZoneId = null);

public sealed record TransactionReportPeriod(
    int Year,
    int Month,
    string TimeZoneId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc
);

public sealed record TransactionReportTotals(
    decimal NetTotal,
    decimal VatTotal,
    decimal GrossTotal,
    int RowCount
);

public sealed record TransactionReportRowResponse(
    Guid Id,
    Guid LineageId,
    int? Version,
    Guid? CorrectsTransactionId,
    Guid CardId,
    Guid? VehicleId,
    string VehiclePlate,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset UserOccurredAt,
    DateTimeOffset PosOccurredAtLocal,
    string PosTimeZoneId,
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

public sealed record TransactionReportResponse(
    int Year,
    int Month,
    string TimeZoneId,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    IReadOnlyList<TransactionReportRowResponse> Transactions,
    TransactionReportTotals Totals
);

public sealed record DashboardSummaryResponse(
    int ActiveVehicleCount,
    int ActiveCardCount,
    TransactionReportTotals PeriodTotals,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc
);

public sealed record FleetCardProgramResponse(
    Guid Id,
    string Name,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveTo
);
