using Microsoft.EntityFrameworkCore;
using Moq;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;

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
        IEnumerable<DailyPatientFeedback>? dailyPatientFeedbacks = null)
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
            context.UserExercises.AddRange(userExercises);
            context.SaveChanges();
        }

        if (doctorProfiles is not null)
        {
            context.DoctorProfiles.AddRange(doctorProfiles);
            context.SaveChanges();
        }

        if (doctorPatients is not null)
        {
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

        return mock;
    }

    public static AppDbContext Create(IEnumerable<UserExercise>? userExercises = null) =>
        CreateMock(userExercises: userExercises).Object;
}
