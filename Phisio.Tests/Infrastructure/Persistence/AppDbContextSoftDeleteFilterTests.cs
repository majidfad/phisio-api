using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Tests.Infrastructure.Persistence;

public class AppDbContextSoftDeleteFilterTests
{
    [Fact]
    public async Task Queries_ExcludeDisabledExercisesByDefault()
    {
        // Arrange
        await using var context = CreateContext();
        var enabled = new Exercise
        {
            ExerciseId = Guid.NewGuid(),
            Title = "Enabled Exercise",
            Description = "Active",
        };
        var disabled = new Exercise
        {
            ExerciseId = Guid.NewGuid(),
            Title = "Disabled Exercise",
            Description = "Inactive",
            IsEnabled = false,
        };

        context.Exercises.AddRange(enabled, disabled);
        await context.SaveChangesAsync();

        // Act
        var visible = await context.Exercises.ToListAsync();
        var all = await context.Exercises.IgnoreQueryFilters().ToListAsync();

        // Assert
        visible.Should().ContainSingle().Which.ExerciseId.Should().Be(enabled.ExerciseId);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Queries_ExcludeDisabledUsersByDefault()
    {
        // Arrange
        await using var context = CreateContext();
        var enabled = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Active Patient",
            Role = UserRole.Patient,
            PhoneNumber = "+15551111111",
            UserName = "+15551111111",
            NormalizedUserName = "+15551111111",
        };
        var disabled = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Disabled Patient",
            Role = UserRole.Patient,
            PhoneNumber = "+15552222222",
            UserName = "+15552222222",
            NormalizedUserName = "+15552222222",
            IsEnabled = false,
        };

        context.Users.AddRange(enabled, disabled);
        await context.SaveChangesAsync();

        // Act
        var visible = await context.Users.ToListAsync();
        var all = await context.Users.IgnoreQueryFilters().ToListAsync();

        // Assert
        visible.Should().ContainSingle().Which.Id.Should().Be(enabled.Id);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Queries_ExcludeDisabledDoctorPatientsByDefault()
    {
        // Arrange
        await using var context = CreateContext();
        var doctorId = Guid.NewGuid();
        var activePatientId = Guid.NewGuid();
        var inactivePatientId = Guid.NewGuid();

        context.DoctorPatients.AddRange(
            new DoctorPatient
            {
                DoctorId = doctorId,
                PatientId = activePatientId,
                IsEnabled = true,
            },
            new DoctorPatient
            {
                DoctorId = doctorId,
                PatientId = inactivePatientId,
                IsEnabled = false,
            });

        await context.SaveChangesAsync();

        // Act
        var visible = await context.DoctorPatients.ToListAsync();
        var all = await context.DoctorPatients.IgnoreQueryFilters().ToListAsync();

        // Assert
        visible.Should().ContainSingle().Which.PatientId.Should().Be(activePatientId);
        all.Should().HaveCount(2);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
