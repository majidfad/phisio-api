using Microsoft.EntityFrameworkCore;
using Moq;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.MockFactory;

internal static class AppDbContextMockFactory
{
    public static Mock<AppDbContext> CreateMock(
        IEnumerable<ApplicationUser>? users = null,
        IEnumerable<Exercise>? exercises = null,
        IEnumerable<Article>? articles = null,
        IEnumerable<UserExercise>? userExercises = null,
        IEnumerable<DoctorProfile>? doctorProfiles = null,
        IEnumerable<DoctorPatient>? doctorPatients = null,
        IEnumerable<ExerciseCompletion>? exerciseCompletions = null,
        IEnumerable<DailyPatientFeedback>? dailyPatientFeedbacks = null,
        IEnumerable<ExerciseProgram>? exercisePrograms = null,
        IEnumerable<Clinic>? clinics = null,
        IEnumerable<ClinicDoctor>? clinicDoctors = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mock = new Mock<AppDbContext>(options) { CallBase = true };
        var context = mock.Object;

        if (users is not null)
        {
            context.Users.AddRange(users);
            context.SaveChanges();
        }

        if (exercises is not null)
        {
            context.Exercises.AddRange(exercises);
            context.SaveChanges();
        }

        if (articles is not null)
        {
            context.Articles.AddRange(articles);
            context.SaveChanges();
        }

        if (userExercises is not null)
        {
            EnsureClinicsForAssignments(context, userExercises);
            context.UserExercises.AddRange(userExercises);
            context.SaveChanges();
        }

        if (doctorProfiles is not null)
        {
            context.DoctorProfiles.AddRange(doctorProfiles);
            context.SaveChanges();
        }

        if (clinics is not null)
        {
            context.Clinics.AddRange(clinics);
            context.SaveChanges();
        }

        if (clinicDoctors is not null)
        {
            context.ClinicDoctors.AddRange(clinicDoctors);
            context.SaveChanges();
        }

        if (doctorPatients is not null)
        {
            EnsureClinicMembershipsForDoctorPatients(context, doctorPatients);
            context.DoctorPatients.AddRange(doctorPatients);
            context.SaveChanges();
        }

        if (exerciseCompletions is not null)
        {
            context.ExerciseCompletions.AddRange(exerciseCompletions);
            context.SaveChanges();
        }

        if (dailyPatientFeedbacks is not null)
        {
            context.DailyPatientFeedbacks.AddRange(dailyPatientFeedbacks);
            context.SaveChanges();
        }

        if (exercisePrograms is not null)
        {
            EnsureClinicsForPrograms(context, exercisePrograms);
            context.ExercisePrograms.AddRange(exercisePrograms);
            context.SaveChanges();
        }

        return mock;
    }

    private static void EnsureClinicsForAssignments(
        AppDbContext context,
        IEnumerable<UserExercise> assignments)
    {
        EnsureClinicsExist(context, assignments.Select(assignment => assignment.ClinicId));
    }

    private static void EnsureClinicsForPrograms(
        AppDbContext context,
        IEnumerable<ExerciseProgram> programs)
    {
        EnsureClinicsExist(context, programs.Select(program => program.ClinicId));
    }

    private static void EnsureClinicsExist(AppDbContext context, IEnumerable<Guid> clinicIds)
    {
        var existingClinicIds = context.Clinics.Select(clinic => clinic.ClinicId).ToHashSet();
        foreach (var clinicId in clinicIds.Distinct())
        {
            if (clinicId == Guid.Empty || existingClinicIds.Contains(clinicId))
            {
                continue;
            }

            context.Clinics.Add(ClinicBuilder.Create(clinicId));
            existingClinicIds.Add(clinicId);
        }

        if (context.ChangeTracker.HasChanges())
        {
            context.SaveChanges();
        }
    }

    private static void EnsureClinicMembershipsForDoctorPatients(
        AppDbContext context,
        IEnumerable<DoctorPatient> doctorPatients)
    {
        var existingClinicIds = context.Clinics.Select(clinic => clinic.ClinicId).ToHashSet();
        var existingMemberships = context.ClinicDoctors
            .AsEnumerable()
            .Select(membership => $"{membership.ClinicId}:{membership.DoctorId}")
            .ToHashSet();

        foreach (var relationship in doctorPatients)
        {
            if (!existingClinicIds.Contains(relationship.ClinicId))
            {
                context.Clinics.Add(ClinicBuilder.Create(relationship.ClinicId, relationship.DoctorId));
                existingClinicIds.Add(relationship.ClinicId);
            }

            var membershipKey = $"{relationship.ClinicId}:{relationship.DoctorId}";
            if (!existingMemberships.Contains(membershipKey))
            {
                context.ClinicDoctors.Add(
                    ClinicBuilder.CreateMembership(relationship.ClinicId, relationship.DoctorId));
                existingMemberships.Add(membershipKey);
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            context.SaveChanges();
        }
    }

    public static AppDbContext Create(IEnumerable<UserExercise>? userExercises = null) =>
        CreateMock(userExercises: userExercises).Object;
}
