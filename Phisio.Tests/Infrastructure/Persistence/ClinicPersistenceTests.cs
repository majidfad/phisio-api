using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Persistence;

public class ClinicPersistenceTests
{
    [Fact]
    public async Task SaveChangesAsync_Clinic_HasExactlyOneClinicManager()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        context.Users.Add(manager);

        var clinic = CreateClinic(manager.Id, "North Clinic", "123 Main St");
        context.Clinics.Add(clinic);

        await context.SaveChangesAsync();

        var saved = await context.Clinics.SingleAsync();
        saved.ClinicManagerId.Should().Be(manager.Id);
        saved.Name.Should().Be("North Clinic");
        saved.Address.Should().Be("123 Main St");
    }

    [Fact]
    public async Task SaveChangesAsync_OneClinicManager_CanManageMultipleClinics()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        context.Users.Add(manager);

        var firstClinic = CreateClinic(manager.Id, "Clinic A", "Address A");
        var secondClinic = CreateClinic(manager.Id, "Clinic B", "Address B");
        firstClinic.EnsureManagerDoctorMembership();
        secondClinic.EnsureManagerDoctorMembership();
        context.Clinics.AddRange(firstClinic, secondClinic);

        await context.SaveChangesAsync();

        var clinics = await context.Clinics
            .Where(clinic => clinic.ClinicManagerId == manager.Id)
            .ToListAsync();

        clinics.Should().HaveCount(2);
        clinics.Select(clinic => clinic.Name).Should().BeEquivalentTo(["Clinic A", "Clinic B"]);
    }

    [Fact]
    public async Task SaveChangesAsync_ClinicManager_IsDoctorOfEveryManagedClinic()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        context.Users.Add(manager);

        var firstClinic = CreateClinic(manager.Id, "Clinic A", "Address A");
        var secondClinic = CreateClinic(manager.Id, "Clinic B", "Address B");
        firstClinic.EnsureManagerDoctorMembership();
        secondClinic.EnsureManagerDoctorMembership();
        context.Clinics.AddRange(firstClinic, secondClinic);

        await context.SaveChangesAsync();

        var managerDoctorLinks = await context.ClinicDoctors
            .Where(link => link.DoctorId == manager.Id)
            .Select(link => link.ClinicId)
            .ToListAsync();

        managerDoctorLinks.Should().BeEquivalentTo([firstClinic.ClinicId, secondClinic.ClinicId]);
    }

    [Fact]
    public async Task SaveChangesAsync_ClinicManagerDoctorMembership_PersistsWithOtherDoctors()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        var otherDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15554444444");
        context.Users.AddRange(manager, otherDoctor);

        var clinic = CreateClinic(manager.Id, "Shared Clinic", "Shared Address");
        clinic.EnsureManagerDoctorMembership();
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = otherDoctor.Id,
        });
        await context.SaveChangesAsync();

        var doctorIds = await context.ClinicDoctors
            .Where(link => link.ClinicId == clinic.ClinicId)
            .Select(link => link.DoctorId)
            .ToListAsync();

        doctorIds.Should().BeEquivalentTo([manager.Id, otherDoctor.Id]);
    }

    [Fact]
    public async Task SaveChangesAsync_Clinic_CanHaveMultiplePhoneNumbers()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        context.Users.Add(manager);

        var clinic = CreateClinic(manager.Id, "Central Clinic", "456 Center Rd");
        clinic.PhoneNumbers =
        [
            new ClinicPhoneNumber
            {
                ClinicPhoneNumberId = Guid.NewGuid(),
                PhoneNumber = "+15551111111",
            },
            new ClinicPhoneNumber
            {
                ClinicPhoneNumberId = Guid.NewGuid(),
                PhoneNumber = "+15552222222",
            },
        ];
        context.Clinics.Add(clinic);

        await context.SaveChangesAsync();

        var phoneNumbers = await context.ClinicPhoneNumbers
            .Where(phoneNumber => phoneNumber.ClinicId == clinic.ClinicId)
            .Select(phoneNumber => phoneNumber.PhoneNumber)
            .ToListAsync();

        phoneNumbers.Should().BeEquivalentTo(["+15551111111", "+15552222222"]);
    }

    [Fact]
    public async Task SaveChangesAsync_Doctor_CanBelongToMultipleClinics()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15553333333");
        context.Users.AddRange(manager, doctor);

        var firstClinic = CreateClinic(manager.Id, "Clinic One", "One Street");
        var secondClinic = CreateClinic(manager.Id, "Clinic Two", "Two Street");
        context.Clinics.AddRange(firstClinic, secondClinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.AddRange(
            new ClinicDoctor { ClinicId = firstClinic.ClinicId, DoctorId = doctor.Id },
            new ClinicDoctor { ClinicId = secondClinic.ClinicId, DoctorId = doctor.Id });

        await context.SaveChangesAsync();

        var clinicIds = await context.ClinicDoctors
            .Where(link => link.DoctorId == doctor.Id)
            .Select(link => link.ClinicId)
            .ToListAsync();

        clinicIds.Should().BeEquivalentTo([firstClinic.ClinicId, secondClinic.ClinicId]);
    }

    [Fact]
    public async Task SaveChangesAsync_Clinic_CanHaveMultipleDoctors()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        var firstDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15554444444");
        var secondDoctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15555555555");
        context.Users.AddRange(manager, firstDoctor, secondDoctor);

        var clinic = CreateClinic(manager.Id, "Shared Clinic", "Shared Address");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.AddRange(
            new ClinicDoctor { ClinicId = clinic.ClinicId, DoctorId = firstDoctor.Id },
            new ClinicDoctor { ClinicId = clinic.ClinicId, DoctorId = secondDoctor.Id });

        await context.SaveChangesAsync();

        var doctorIds = await context.ClinicDoctors
            .Where(link => link.ClinicId == clinic.ClinicId)
            .Select(link => link.DoctorId)
            .ToListAsync();

        doctorIds.Should().BeEquivalentTo([firstDoctor.Id, secondDoctor.Id]);
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateClinicDoctorRelationship_Throws()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15556666666");
        context.Users.AddRange(manager, doctor);

        var clinic = CreateClinic(manager.Id, "Duplicate Test Clinic", "Duplicate Address");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        await context.SaveChangesAsync();

        var act = () => context.ClinicDoctors.Add(new ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveChangesAsync_ClinicPhoneNumber_BelongsToCorrectClinic()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        context.Users.Add(manager);

        var firstClinic = CreateClinic(manager.Id, "First Clinic", "First Address");
        var secondClinic = CreateClinic(manager.Id, "Second Clinic", "Second Address");
        context.Clinics.AddRange(firstClinic, secondClinic);
        await context.SaveChangesAsync();

        var phoneNumber = new ClinicPhoneNumber
        {
            ClinicPhoneNumberId = Guid.NewGuid(),
            ClinicId = firstClinic.ClinicId,
            PhoneNumber = "+15557777777",
        };
        context.ClinicPhoneNumbers.Add(phoneNumber);
        await context.SaveChangesAsync();

        var loaded = await context.ClinicPhoneNumbers
            .Include(number => number.Clinic)
            .SingleAsync(number => number.ClinicPhoneNumberId == phoneNumber.ClinicPhoneNumberId);

        loaded.ClinicId.Should().Be(firstClinic.ClinicId);
        loaded.Clinic.ClinicId.Should().Be(firstClinic.ClinicId);
        loaded.Clinic.Name.Should().Be("First Clinic");

        var secondClinicNumbers = await context.ClinicPhoneNumbers
            .Where(number => number.ClinicId == secondClinic.ClinicId)
            .ToListAsync();

        secondClinicNumbers.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_ClinicManagerRelationship_UsesApplicationUserForeignKey()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        context.Users.Add(manager);

        var clinic = CreateClinic(manager.Id, "Managed Clinic", "Managed Address");
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var loadedManager = await context.Users
            .Include(user => user.ManagedClinics)
            .SingleAsync(user => user.Id == manager.Id);

        loadedManager.ManagedClinics.Should().ContainSingle()
            .Which.ClinicId.Should().Be(clinic.ClinicId);
    }

    [Fact]
    public void Model_ClinicManagerId_DoesNotHaveUniqueIndex()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Clinic));
        entityType.Should().NotBeNull();

        var clinicManagerIndex = entityType!.GetIndexes()
            .SingleOrDefault(index =>
                index.Properties.Count == 1
                && index.Properties[0].Name == nameof(Clinic.ClinicManagerId));

        clinicManagerIndex.Should().NotBeNull();
        clinicManagerIndex!.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void Model_NormalizedClinicPhoneNumber_HasUniqueIndex()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ClinicPhoneNumber));
        entityType.Should().NotBeNull();

        var index = entityType!.GetIndexes()
            .Single(item =>
                item.Properties.Count == 1
                && item.Properties[0].Name == nameof(ClinicPhoneNumber.NormalizedPhoneNumber));

        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Model_ClinicManagerRelationship_UsesRestrictDeleteBehavior()
    {
        using var context = CreateContext();
        var clinicEntity = context.Model.FindEntityType(typeof(Clinic));
        clinicEntity.Should().NotBeNull();

        var clinicManagerForeignKey = clinicEntity!.GetForeignKeys()
            .Single(fk => fk.Properties.Single().Name == nameof(Clinic.ClinicManagerId));

        clinicManagerForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public async Task SaveChangesAsync_DeletingClinic_CascadesPhoneNumbersAndDoctorLinks()
    {
        await using var context = CreateContext();
        var manager = CreateClinicManager();
        var doctor = ApplicationUserBuilder.Doctor(phoneNumber: "+15558888888");
        context.Users.AddRange(manager, doctor);

        var clinic = CreateClinic(manager.Id, "Cascade Clinic", "Cascade Address");
        clinic.PhoneNumbers =
        [
            new ClinicPhoneNumber
            {
                ClinicPhoneNumberId = Guid.NewGuid(),
                PhoneNumber = "+15559999999",
            },
        ];
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        context.ClinicDoctors.Add(new ClinicDoctor
        {
            ClinicId = clinic.ClinicId,
            DoctorId = doctor.Id,
        });
        await context.SaveChangesAsync();

        context.Clinics.Remove(clinic);
        await context.SaveChangesAsync();

        (await context.ClinicPhoneNumbers.CountAsync()).Should().Be(0);
        (await context.ClinicDoctors.CountAsync()).Should().Be(0);
        (await context.Users.CountAsync(user => user.Id == doctor.Id)).Should().Be(1);
    }

    private static ApplicationUser CreateClinicManager() =>
        ApplicationUserBuilder.ClinicManager(phoneNumber: $"+1555{Random.Shared.Next(1000000, 9999999)}");

    private static Clinic CreateClinic(Guid clinicManagerId, string name, string address) =>
        new()
        {
            ClinicId = Guid.NewGuid(),
            ClinicManagerId = clinicManagerId,
            Name = name,
            Address = address,
        };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
