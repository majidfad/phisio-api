using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Tests.Infrastructure.Persistence;

public class DoctorPatientPersistenceTests
{
    [Fact]
    public void Model_DoctorPatient_HasCompositePrimaryKeyIncludingClinicId()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(DoctorPatient));
        entity.Should().NotBeNull();

        entity!.FindPrimaryKey()!.Properties
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [
                    nameof(DoctorPatient.DoctorId),
                    nameof(DoctorPatient.PatientId),
                    nameof(DoctorPatient.ClinicId),
                ],
                options => options.WithStrictOrdering());
    }

    [Fact]
    public void Model_DoctorPatient_HasUniqueIndexOnPatientDoctorClinic()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(DoctorPatient));
        entity.Should().NotBeNull();

        var index = entity!.GetIndexes().Single(item =>
            item.Properties.Select(property => property.Name)
                .SequenceEqual(
                [
                    nameof(DoctorPatient.PatientId),
                    nameof(DoctorPatient.DoctorId),
                    nameof(DoctorPatient.ClinicId),
                ]));

        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Model_DoctorPatient_ClinicForeignKey_UsesRestrictDeleteBehavior()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(DoctorPatient));
        entity.Should().NotBeNull();

        var clinicForeignKey = entity!.GetForeignKeys()
            .Single(fk => fk.Properties.Single().Name == nameof(DoctorPatient.ClinicId));

        clinicForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        clinicForeignKey.PrincipalEntityType.ClrType.Should().Be(typeof(Clinic));
        clinicForeignKey.IsRequired.Should().BeTrue();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
