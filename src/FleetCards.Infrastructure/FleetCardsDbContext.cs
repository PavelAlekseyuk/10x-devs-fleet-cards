using FleetCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Infrastructure;

public sealed class FleetCardsDbContext(DbContextOptions<FleetCardsDbContext> options)
    : DbContext(options)
{
    public DbSet<FleetUser> Users => Set<FleetUser>();
    public DbSet<FleetClient> Clients => Set<FleetClient>();
    public DbSet<FleetCar> Cars => Set<FleetCar>();
    public DbSet<FleetAuditEvent> AuditEvents => Set<FleetAuditEvent>();
    public DbSet<FleetCard> Cards => Set<FleetCard>();
    public DbSet<FleetCardAssignmentEvent> CardAssignmentEvents => Set<FleetCardAssignmentEvent>();
    public DbSet<FuelTransaction> Transactions => Set<FuelTransaction>();
    public DbSet<FleetCardProgram> Programs => Set<FleetCardProgram>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FleetUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.UserName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Permission).HasMaxLength(20).IsRequired();
            entity.HasIndex(user => user.UserName).IsUnique();
        });

        modelBuilder.Entity<FleetClient>(entity =>
        {
            entity.HasKey(client => client.Id);
            entity.Property(client => client.LegalName).HasMaxLength(200).IsRequired();
            entity.Property(client => client.Nip).HasMaxLength(20).IsRequired();
            entity.Property(client => client.Jurisdiction).HasMaxLength(100).IsRequired();
            entity.Property(client => client.Address).HasMaxLength(300).IsRequired();
            entity.HasIndex(client => client.Nip).IsUnique();
        });

        modelBuilder.Entity<FleetCar>(entity =>
        {
            entity.HasKey(car => car.Id);
            entity.Property(car => car.RegistrationPlate).HasMaxLength(20).IsRequired();
            entity.Property(car => car.NormalizedRegistrationPlate).HasMaxLength(20).IsRequired();
            entity.Property(car => car.FuelType).HasMaxLength(10).IsRequired();
            entity.Property(car => car.Make).HasMaxLength(100);
            entity.Property(car => car.Model).HasMaxLength(100);
            entity
                .HasIndex(car => new { car.ClientId, car.NormalizedRegistrationPlate })
                .IsUnique()
                .HasFilter("[IsArchived] = 0");
            entity
                .HasOne<FleetClient>()
                .WithMany()
                .HasForeignKey(car => car.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FleetAuditEvent>(entity =>
        {
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.Actor).HasMaxLength(200).IsRequired();
            entity.Property(auditEvent => auditEvent.Action).HasMaxLength(100).IsRequired();
            entity.Property(auditEvent => auditEvent.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(auditEvent => auditEvent.Succeeded).IsRequired();
            entity.Property(auditEvent => auditEvent.OccurredAt).IsRequired();
            entity.ToTable("AuditEvents");
        });

        modelBuilder.Entity<FleetCard>(entity =>
        {
            entity.HasKey(card => card.Id);
            entity.Property(card => card.MaskedPan).HasMaxLength(24).IsRequired();
            entity.Property(card => card.ProviderToken).HasMaxLength(200).IsRequired();
            entity
                .Property(card => card.State)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.HasIndex(card => card.ProviderToken).IsUnique();
            entity
                .HasOne<FleetClient>()
                .WithMany()
                .HasForeignKey(card => card.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne<FleetCar>()
                .WithMany()
                .HasForeignKey(card => card.AssignedCarId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FuelTransaction>(entity =>
        {
            entity.HasKey(transaction => transaction.Id);
            entity.HasAlternateKey(transaction => new { transaction.Id, transaction.LineageId });
            entity.Property(transaction => transaction.LineageId).IsRequired();
            entity.Property(transaction => transaction.Version);
            entity.Property(transaction => transaction.CorrectsTransactionId);
            entity.Property(transaction => transaction.VehiclePlate).HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.OccurredAt).IsRequired();
            entity.Property(transaction => transaction.PosOccurredAtLocal).IsRequired();
            entity
                .Property(transaction => transaction.PosTimeZoneId)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(transaction => transaction.StationName).HasMaxLength(200).IsRequired();
            entity.Property(transaction => transaction.FuelType).HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.Quantity).HasPrecision(18, 3);
            entity.Property(transaction => transaction.UnitPrice).HasPrecision(18, 4);
            entity.Property(transaction => transaction.Discount).HasPrecision(18, 4);
            entity.Property(transaction => transaction.NetTotal).HasPrecision(18, 2);
            entity.Property(transaction => transaction.VatRate).HasPrecision(9, 4);
            entity.Property(transaction => transaction.VatAmount).HasPrecision(18, 2);
            entity.Property(transaction => transaction.GrossTotal).HasPrecision(18, 2);
            entity
                .Property(transaction => transaction.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.HasIndex(transaction => transaction.OccurredAt);
            entity
                .HasIndex(transaction => new { transaction.LineageId, transaction.Version })
                .IsUnique()
                .HasFilter("[Version] IS NOT NULL");
            entity.ToTable(
                "Transactions",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_Transactions_OccurredAtUtc",
                        "DATEPART(TZOFFSET, [OccurredAt]) = 0"
                    );
                    table.HasCheckConstraint(
                        "CK_Transactions_LineageVersion",
                        "([Status] = 'Completed' AND [Id] = [LineageId] AND [Version] = 1 AND [CorrectsTransactionId] IS NULL) OR "
                            + "([Status] = 'Recalculated' AND [Id] <> [LineageId] AND [Version] > 1 AND [CorrectsTransactionId] = [LineageId]) OR "
                            + "([Status] IN ('Refunded', 'Cancelled') AND [Id] <> [LineageId] AND [Version] IS NULL AND [CorrectsTransactionId] = [LineageId])"
                    );
                }
            );
            entity
                .HasOne<FleetCard>()
                .WithMany()
                .HasForeignKey(transaction => transaction.CardId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne<FleetCar>()
                .WithMany()
                .HasForeignKey(transaction => transaction.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne<FuelTransaction>()
                .WithMany()
                .HasForeignKey(transaction => new
                {
                    transaction.CorrectsTransactionId,
                    transaction.LineageId,
                })
                .HasPrincipalKey(transaction => new { transaction.Id, transaction.LineageId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FleetCardAssignmentEvent>(entity =>
        {
            entity.HasKey(assignment => assignment.Id);
            entity.Property(assignment => assignment.EffectiveAt).IsRequired();
            entity.Property(assignment => assignment.OrderingKey).IsRequired();
            entity
                .HasIndex(assignment => new { assignment.CardId, assignment.OrderingKey })
                .IsUnique();
            entity.HasIndex(assignment => new
            {
                assignment.CardId,
                assignment.EffectiveAt,
                assignment.OrderingKey,
            });
            entity
                .HasOne<FleetCard>()
                .WithMany()
                .HasForeignKey(assignment => assignment.CardId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne<FleetCar>()
                .WithMany()
                .HasForeignKey(assignment => assignment.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("CardAssignmentEvents");
        });

        modelBuilder.Entity<FleetCardProgram>(entity =>
        {
            entity.HasKey(program => program.Id);
            entity.Property(program => program.Name).HasMaxLength(200).IsRequired();
        });

        AddSeedData(modelBuilder);
    }

    private static void AddSeedData(ModelBuilder modelBuilder)
    {
        var clientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var primaryCarId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cardId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder
            .Entity<FleetUser>()
            .HasData(
                new FleetUser
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    UserName = "anna.nowak",
                    PasswordHash =
                        "pbkdf2-sha256:120000:32:YW5uYS5ub3dha3xmbGVldGNhcmRzfHYx:ii5CUBhy/r7ijfx9rxha1fx84ADBy5blrUopRVSUQOE=",
                    Permission = "ReadWrite",
                },
                new FleetUser
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    UserName = "piotr.wisniewski",
                    PasswordHash =
                        "pbkdf2-sha256:120000:32:cGlvdHIud2lzbmlld3NraXxmbGVldGNhcmRzfHYx:Jey8SEvmKOAL0FlmRyh51nW+zGLSsICzEqjEb9w9bgQ=",
                    Permission = "ReadOnly",
                }
            );
        modelBuilder
            .Entity<FleetClient>()
            .HasData(
                new FleetClient
                {
                    Id = clientId,
                    LegalName = "Baltic Logistics Sp. z o.o.",
                    Nip = "5250001234",
                    Jurisdiction = "Poland",
                    Address = "ul. Prosta 12, 00-850 Warszawa",
                }
            );
        modelBuilder
            .Entity<FleetCar>()
            .HasData(
                new FleetCar
                {
                    Id = primaryCarId,
                    ClientId = clientId,
                    RegistrationPlate = "WA 12345",
                    NormalizedRegistrationPlate = "WA12345",
                    FuelType = "Diesel",
                    Make = "Volvo",
                    Model = "FH",
                    IsArchived = false,
                },
                new FleetCar
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    ClientId = clientId,
                    RegistrationPlate = "PO 98765",
                    NormalizedRegistrationPlate = "PO98765",
                    FuelType = "95",
                    Make = "Toyota",
                    Model = "Corolla",
                    IsArchived = false,
                }
            );
        modelBuilder
            .Entity<FleetCard>()
            .HasData(
                new FleetCard
                {
                    Id = cardId,
                    ClientId = clientId,
                    MaskedPan = "**** **** **** 4242",
                    ProviderToken = "mock-card-4242",
                    State = CardState.Active,
                    AssignedCarId = primaryCarId,
                }
            );
        modelBuilder
            .Entity<FleetCardAssignmentEvent>()
            .HasData(
                new FleetCardAssignmentEvent
                {
                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    CardId = cardId,
                    VehicleId = primaryCarId,
                    EffectiveAt = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
                    OrderingKey = 1,
                }
            );
        modelBuilder
            .Entity<FuelTransaction>()
            .HasData(
                new FuelTransaction
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    LineageId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Version = 1,
                    CardId = cardId,
                    VehicleId = primaryCarId,
                    VehiclePlate = "WA 12345",
                    OccurredAt = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
                    PosOccurredAtLocal = new DateTimeOffset(
                        2026,
                        9,
                        9,
                        14,
                        0,
                        0,
                        TimeSpan.FromHours(2)
                    ),
                    PosTimeZoneId = "Europe/Warsaw",
                    StationName = "angry storage",
                    FuelType = "Diesel",
                    Quantity = 42.081m,
                    UnitPrice = 6.49m,
                    Discount = 0.18m,
                    NetTotal = 265.53m,
                    VatRate = 0.23m,
                    VatAmount = 61.07m,
                    GrossTotal = 326.60m,
                    Status = TransactionStatus.Completed,
                }
            );
        modelBuilder
            .Entity<FleetCardProgram>()
            .HasData(
                new FleetCardProgram
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    Name = "Baltic Standard",
                    ActiveFrom = new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero),
                }
            );
    }
}
