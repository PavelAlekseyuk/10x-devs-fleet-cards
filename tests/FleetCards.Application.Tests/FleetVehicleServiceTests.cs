using FleetCards.Application;
using FleetCards.Contracts;

namespace FleetCards.Application.Tests;

public sealed class FleetVehicleServiceTests
{
    [Theory]
    [InlineData(" wa 12\t345 ", "WA12345")]
    [InlineData("\r ab\u2003123\n", "AB123")]
    public void Registration_plate_identity_ignores_case_and_all_whitespace(
        string registrationPlate,
        string expected
    )
    {
        Assert.Equal(expected, FleetVehicleRules.NormalizeRegistrationPlate(registrationPlate));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task Create_rejects_blank_registration_plates(string registrationPlate)
    {
        var service = new SeededFleetVehicleService();

        var result = await service.CreateAsync(
            new CreateFleetCarRequest(registrationPlate, "95", null, null),
            "anna.nowak",
            "test-correlation"
        );

        Assert.Equal(VehicleCommandError.Validation, result.Error);
        Assert.Contains("RegistrationPlate", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_registration_plates_over_twenty_trimmed_characters()
    {
        var service = new SeededFleetVehicleService();

        var result = await service.CreateAsync(
            new CreateFleetCarRequest("123456789012345678901", "95", null, null),
            "anna.nowak",
            "test-correlation"
        );

        Assert.Equal(VehicleCommandError.Validation, result.Error);
        Assert.Contains("RegistrationPlate", result.ValidationErrors!.Keys);
    }

    [Theory]
    [InlineData("95")]
    [InlineData("98")]
    [InlineData("Diesel")]
    [InlineData("95+")]
    [InlineData("98+")]
    [InlineData("Diesel+")]
    public async Task Create_accepts_each_supported_fuel_type(string fuelType)
    {
        var service = new SeededFleetVehicleService();

        var result = await service.CreateAsync(
            new CreateFleetCarRequest($"PL-{fuelType}", fuelType, null, null),
            "anna.nowak",
            "test-correlation"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(fuelType, result.Vehicle!.FuelType);
    }

    [Theory]
    [InlineData("diesel")]
    [InlineData("Diesel ")]
    [InlineData("E85")]
    public async Task Create_rejects_unsupported_or_non_exact_fuel_types(string fuelType)
    {
        var service = new SeededFleetVehicleService();

        var result = await service.CreateAsync(
            new CreateFleetCarRequest("PL-TEST", fuelType, null, null),
            "anna.nowak",
            "test-correlation"
        );

        Assert.Equal(VehicleCommandError.Validation, result.Error);
        Assert.Contains("FuelType", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Create_trims_display_and_optional_descriptive_fields_and_search_is_normalized()
    {
        var service = new SeededFleetVehicleService();

        var result = await service.CreateAsync(
            new CreateFleetCarRequest(" ab \t123 ", "Diesel", " Volvo ", " "),
            "anna.nowak",
            "test-correlation"
        );
        var found = await service.GetCarsAsync(" A B 1\t2 3 ");

        Assert.True(result.IsSuccess);
        Assert.Equal("ab \t123", result.Vehicle!.RegistrationPlate);
        Assert.Equal("Volvo", result.Vehicle.Make);
        Assert.Null(result.Vehicle.Model);
        Assert.Contains(found, car => car.Id == result.Vehicle.Id);
        Assert.DoesNotContain("NormalizedRegistrationPlate", result.Vehicle.ToString());
    }

    [Fact]
    public async Task Active_plates_are_unique_but_archived_plates_can_be_reused_and_not_restored_into_conflict()
    {
        var service = new SeededFleetVehicleService();
        var original = await service.CreateAsync(
            new CreateFleetCarRequest("xy 12", "95", null, null),
            "anna.nowak",
            "create-original"
        );
        var duplicate = await service.CreateAsync(
            new CreateFleetCarRequest("XY\t12", "95", null, null),
            "anna.nowak",
            "create-duplicate"
        );

        Assert.Equal(VehicleCommandError.Conflict, duplicate.Error);
        var archived = await service.ArchiveAsync(
            original.Vehicle!.Id,
            "anna.nowak",
            "archive-original"
        );
        var replacement = await service.CreateAsync(
            new CreateFleetCarRequest("XY12", "98", null, null),
            "anna.nowak",
            "create-replacement"
        );
        var restore = await service.RestoreAsync(
            original.Vehicle.Id,
            "anna.nowak",
            "restore-original"
        );
        var archivedOriginal = (await service.GetCarsAsync("XY12", includeArchived: true)).Single(
            car => car.Id == original.Vehicle.Id
        );

        Assert.True(archived.IsSuccess);
        Assert.True(replacement.IsSuccess);
        Assert.Equal(VehicleCommandError.Conflict, restore.Error);
        Assert.True(archivedOriginal.IsArchived);
        Assert.True(await service.IsEligibleForAssignmentAsync(replacement.Vehicle!.Id));
        Assert.False(await service.IsEligibleForAssignmentAsync(original.Vehicle.Id));
        Assert.False(await service.IsEligibleForAssignmentAsync(Guid.NewGuid()));
    }
}
