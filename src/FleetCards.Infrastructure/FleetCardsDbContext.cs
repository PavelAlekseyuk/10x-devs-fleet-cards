using FleetCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Infrastructure;

public sealed class FleetCardsDbContext(DbContextOptions<FleetCardsDbContext> options)
    : DbContext(options)
{
    public DbSet<FleetUser> Users => Set<FleetUser>();
    public DbSet<FleetClient> Clients => Set<FleetClient>();
    public DbSet<FleetCar> Cars => Set<FleetCar>();
    public DbSet<FleetCard> Cards => Set<FleetCard>();
    public DbSet<FuelTransaction> Transactions => Set<FuelTransaction>();
    public DbSet<FleetCardProgram> Programs => Set<FleetCardProgram>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FleetUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.UserName).HasMaxLength(100).IsRequired();
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
            entity.Property(car => car.Make).HasMaxLength(100);
            entity.Property(car => car.Model).HasMaxLength(100);
            entity.HasIndex(car => new { car.ClientId, car.NormalizedRegistrationPlate }).IsUnique();
            entity.HasOne<FleetClient>().WithMany().HasForeignKey(car => car.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FleetCard>(entity =>
        {
            entity.HasKey(card => card.Id);
            entity.Property(card => card.MaskedPan).HasMaxLength(24).IsRequired();
            entity.Property(card => card.ProviderToken).HasMaxLength(200).IsRequired();
            entity.Property(card => card.State).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(card => card.ProviderToken).IsUnique();
            entity.HasOne<FleetClient>().WithMany().HasForeignKey(card => card.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FleetCar>().WithMany().HasForeignKey(card => card.AssignedCarId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FuelTransaction>(entity =>
        {
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.VehiclePlate).HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.StationName).HasMaxLength(200).IsRequired();
            entity.Property(transaction => transaction.FuelType).HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.Quantity).HasPrecision(18, 3);
            entity.Property(transaction => transaction.UnitPrice).HasPrecision(18, 4);
            entity.Property(transaction => transaction.Discount).HasPrecision(18, 4);
            entity.Property(transaction => transaction.NetTotal).HasPrecision(18, 2);
            entity.Property(transaction => transaction.VatRate).HasPrecision(9, 4);
            entity.Property(transaction => transaction.VatAmount).HasPrecision(18, 2);
            entity.Property(transaction => transaction.GrossTotal).HasPrecision(18, 2);
            entity.Property(transaction => transaction.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(transaction => transaction.OccurredAt);
            entity.HasOne<FleetCard>().WithMany().HasForeignKey(transaction => transaction.CardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FleetCar>().WithMany().HasForeignKey(transaction => transaction.VehicleId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<FleetUser>().HasData(
            new FleetUser
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                UserName = "anna.nowak",
                Permission = "ReadWrite",
            }
        );
        modelBuilder.Entity<FleetClient>().HasData(
            new FleetClient
            {
                Id = clientId,
                LegalName = "Baltic Logistics Sp. z o.o.",
                Nip = "5250001234",
                Jurisdiction = "Poland",
                Address = "ul. Prosta 12, 00-850 Warszawa",
            }
        );
        modelBuilder.Entity<FleetCar>().HasData(
            new FleetCar
            {
                Id = primaryCarId,
                ClientId = clientId,
                RegistrationPlate = "WA 12345",
                NormalizedRegistrationPlate = "WA12345",
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
                Make = "Toyota",
                Model = "Corolla",
                IsArchived = false,
            }
        );
        modelBuilder.Entity<FleetCard>().HasData(
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
        modelBuilder.Entity<FuelTransaction>().HasData(
            new FuelTransaction
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                CardId = cardId,
                VehicleId = primaryCarId,
                VehiclePlate = "WA 12345",
                OccurredAt = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
                StationName = "angry storage",
                FuelType = "Diesel",
                Quantity = 42.125m,
                UnitPrice = 6.49m,
                Discount = 0.18m,
                NetTotal = 265.53m,
                VatRate = 0.23m,
                VatAmount = 61.07m,
                GrossTotal = 326.60m,
                Status = TransactionStatus.Completed,
            }
        );
        modelBuilder.Entity<FleetCardProgram>().HasData(
            new FleetCardProgram
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Baltic Standard",
                ActiveFrom = new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero),
            }
        );
    }
}