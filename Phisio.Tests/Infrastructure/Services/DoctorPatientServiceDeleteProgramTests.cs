using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceDeleteProgramTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    [Fact]
    public async Task DeleteProgramAsync_WhenProgramMissing_ReturnsNotFound()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)]);
        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.DeleteProgramAsync(doctor.Id, patient.Id, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(DoctorPatientErrors.ProgramNotFound);
    }

    [Fact]
    public async Task DeleteProgramAsync_CancelsFutureIncomplete_KeepsPastAndCompletedToday()
    {
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(createdByDoctorId: doctor.Id);
        var programId = Guid.NewGuid();

        var program = new ExerciseProgram
        {
            ProgramId = programId,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartDate = Yesterday,
            EndDate = Tomorrow,
            CadenceType = ExerciseProgramCadenceType.DaysOfWeek,
            DaysOfWeekMask = 0b1111111,
            IsEnabled = true,
            Exercises =
            [
                new ProgramExercise
                {
                    ProgramExerciseId = Guid.NewGuid(),
                    ProgramId = programId,
                    ExerciseId = exercise.ExerciseId,
                    IsEnabled = true,
                },
            ],
        };

        var pastAssignment = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Yesterday);
        pastAssignment.ProgramId = programId;

        var todayIncomplete = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        todayIncomplete.ProgramId = programId;

        var todayCompleted = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        todayCompleted.ProgramId = programId;

        var futureAssignment = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Tomorrow);
        futureAssignment.ProgramId = programId;

        var completion = ExerciseCompletionBuilder.Create(
            todayCompleted.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            exercisePrograms: [program],
            userExercises: [pastAssignment, todayIncomplete, todayCompleted, futureAssignment],
            exerciseCompletions: [completion]);

        var sut = new DoctorPatientService(dbContext.Object);

        var result = await sut.DeleteProgramAsync(doctor.Id, patient.Id, programId);

        result.Succeeded.Should().BeTrue();

        var ctx = dbContext.Object;
        var storedProgram = await ctx.ExercisePrograms
            .IgnoreQueryFilters()
            .SingleAsync(p => p.ProgramId == programId);
        storedProgram.IsEnabled.Should().BeFalse();

        var programExercises = await ctx.ProgramExercises
            .IgnoreQueryFilters()
            .Where(e => e.ProgramId == programId)
            .ToListAsync();
        programExercises.Should().OnlyContain(e => !e.IsEnabled);

        var past = await ctx.UserExercises.IgnoreQueryFilters()
            .SingleAsync(ue => ue.UserExerciseId == pastAssignment.UserExerciseId);
        past.IsActive.Should().BeTrue();
        past.IsEnabled.Should().BeTrue();

        var incompleteToday = await ctx.UserExercises.IgnoreQueryFilters()
            .SingleAsync(ue => ue.UserExerciseId == todayIncomplete.UserExerciseId);
        incompleteToday.IsActive.Should().BeFalse();
        incompleteToday.IsEnabled.Should().BeFalse();

        var completedToday = await ctx.UserExercises.IgnoreQueryFilters()
            .SingleAsync(ue => ue.UserExerciseId == todayCompleted.UserExerciseId);
        completedToday.IsActive.Should().BeTrue();
        completedToday.IsEnabled.Should().BeTrue();

        var future = await ctx.UserExercises.IgnoreQueryFilters()
            .SingleAsync(ue => ue.UserExerciseId == futureAssignment.UserExerciseId);
        future.IsActive.Should().BeFalse();
        future.IsEnabled.Should().BeFalse();
    }
}
