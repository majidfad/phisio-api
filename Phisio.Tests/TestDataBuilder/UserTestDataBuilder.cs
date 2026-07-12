using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

namespace Phisio.Tests.TestDataBuilder;

internal static class ApplicationUserBuilder
{
    public static ApplicationUser Doctor(
        string name = "Dr. Jane Smith",
        string phoneNumber = "+15551234567",
        string? email = "jane.smith@example.com",
        Guid? id = null) =>
        CreateUser(name, UserRole.Doctor, phoneNumber, email, id);

    public static ApplicationUser Patient(
        string name = "John Patient",
        string phoneNumber = "+15557654321",
        Guid? id = null) =>
        CreateUser(name, UserRole.Patient, phoneNumber, email: null, id);

    public static ApplicationUser Admin(
        string name = "System Administrator",
        string phoneNumber = "+10000000000",
        string? email = "admin@phisio.com",
        Guid? id = null) =>
        CreateUser(name, UserRole.Admin, phoneNumber, email, id);

    private static ApplicationUser CreateUser(
        string name,
        UserRole role,
        string phoneNumber,
        string? email,
        Guid? id) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Role = role,
            PhoneNumber = phoneNumber,
            UserName = phoneNumber,
            NormalizedUserName = phoneNumber.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email?.ToUpperInvariant()
        };
}

internal static class UserExerciseBuilder
{
    public static UserExercise ForUser(
        Guid userId,
        bool asDoctor = true,
        Guid? exerciseId = null) =>
        new()
        {
            UserExerciseId = Guid.NewGuid(),
            DoctorId = asDoctor ? userId : Guid.NewGuid(),
            PatientId = asDoctor ? Guid.NewGuid() : userId,
            ExerciseId = exerciseId ?? Guid.NewGuid(),
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        };
}
