namespace FleetCards.Domain;

public enum CardState
{
    Active,
    Blocked,
    Stolen,
    Archived,
}

public enum TransactionStatus
{
    Completed,
    Refunded,
    Cancelled,
    Recalculated,
}

public sealed class FleetUser
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}

public sealed class FleetClient
{
    public Guid Id { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string Nip { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public sealed class FleetCar
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string RegistrationPlate { get; set; } = string.Empty;
    public string NormalizedRegistrationPlate { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class FleetCard
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string MaskedPan { get; set; } = string.Empty;
    public string ProviderToken { get; set; } = string.Empty;
    public CardState State { get; set; }
    public Guid? AssignedCarId { get; set; }
}

public sealed class FuelTransaction
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public Guid? VehicleId { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal NetTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossTotal { get; set; }
    public TransactionStatus Status { get; set; }
}

public sealed class FleetCardProgram
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset? ActiveTo { get; set; }
}
