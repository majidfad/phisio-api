using FluentAssertions;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class AdminDashboardServiceGetStatsTests
{
    [Fact]
    public async Task GetStatsAsync_ReturnsEnabledCountsByRole()
    {
        // Arrange
        var enabledDoctor = ApplicationUserBuilder.Doctor();
        var disabledDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15550000001");
        disabledDoctor.IsEnabled = false;
        var enabledPatient = ApplicationUserBuilder.Patient();
        var disabledPatient = ApplicationUserBuilder.Patient(phoneNumber: "+15550000002");
        disabledPatient.IsEnabled = false;
        var admin = ApplicationUserBuilder.Admin();
        var enabledExercise = ExerciseBuilder.Create();
        var disabledExercise = ExerciseBuilder.Create(title: "Disabled Stretch", id: Guid.NewGuid());
        disabledExercise.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [enabledDoctor, disabledDoctor, enabledPatient, disabledPatient, admin],
            exercises: [enabledExercise, disabledExercise]);

        var sut = new AdminDashboardService(dbContext.Object);

        // Act
        var result = await sut.GetStatsAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DoctorCount.Should().Be(1);
        result.Value.PatientCount.Should().Be(1);
        result.Value.ExerciseCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_WhenNoDataExists_ReturnsZeros()
    {
        // Arrange
        var sut = new AdminDashboardService(AppDbContextMockFactory.CreateMock().Object);

        // Act
        var result = await sut.GetStatsAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DoctorCount.Should().Be(0);
        result.Value.PatientCount.Should().Be(0);
        result.Value.ExerciseCount.Should().Be(0);
    }
}
