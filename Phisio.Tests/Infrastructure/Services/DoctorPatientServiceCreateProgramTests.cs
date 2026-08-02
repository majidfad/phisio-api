using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorPatientServiceCreateProgramTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private const int EveryDayMask = 0b1111111;

    private static AssignPatientExerciseItem Item(
        Guid exerciseId,
        int? sets = 3,
        string? reps = "10") =>
        new(exerciseId, Sets: sets, Reps: reps, ClinicianNote: null, PatientCue: null);

    private static CreateExerciseProgramRequest DailyRequest(
        DateOnly startDate, DateOnly endDate, params Guid[] exerciseIds) =>
        new(
            startDate,
            endDate,
            ExerciseProgramCadenceType.DaysOfWeek,
            EveryDayMask,
            IntervalDays: null,
            exerciseIds.Select(id => Item(id)).ToList());

    private static CreateExerciseProgramRequest DailyRequestWithItems(
        DateOnly startDate, DateOnly endDate, params AssignPatientExerciseItem[] items) =>
        new(
            startDate,
            endDate,
            ExerciseProgramCadenceType.DaysOfWeek,
            EveryDayMask,
            IntervalDays: null,
            items.ToList());

    private static ExerciseProgram Program(
        Guid doctorId, Guid patientId, bool isEnabled) =>
        new()
        {
            ProgramId = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            StartDate = Yesterday,
            EndDate = Tomorrow,
            CadenceType = ExerciseProgramCadenceType.DaysOfWeek,
            DaysOfWeekMask = EveryDayMask,
            IsEnabled = isEnabled,
        };

    [Fact]
    public async Task CreateProgramAsync_WhenStartDateInPast_OnlyMaterializesFromToday()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(createdByDoctorId: doctor.Id);
        var deletedProgram = Program(doctor.Id, patient.Id, isEnabled: false);

        // Historical assignment kept after the previous program was deleted.
        var pastLeftover = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Yesterday);
        pastLeftover.ProgramId = deletedProgram.ProgramId;

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            exercisePrograms: [deletedProgram],
            userExercises: [pastLeftover]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.CreateProgramAsync(
            doctor.Id, patient.Id, DailyRequest(Yesterday, Tomorrow, exercise.ExerciseId));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.AssignedCount.Should().Be(2);

        var newAssignments = await dbContext.Object.UserExercises
            .Where(ue => ue.ProgramId == result.Value.ProgramId)
            .ToListAsync();
        newAssignments.Select(ue => ue.ScheduledDate)
            .Should().BeEquivalentTo([Today, Tomorrow]);

        var leftover = await dbContext.Object.UserExercises
            .SingleAsync(ue => ue.UserExerciseId == pastLeftover.UserExerciseId);
        leftover.ProgramId.Should().Be(deletedProgram.ProgramId);
    }

    [Fact]
    public async Task CreateProgramAsync_WhenIncompleteLeftoverFromDeletedProgramExistsToday_AdoptsAssignment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(createdByDoctorId: doctor.Id);
        var deletedProgram = Program(doctor.Id, patient.Id, isEnabled: false);

        var todayLeftover = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        todayLeftover.ProgramId = deletedProgram.ProgramId;

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            exercisePrograms: [deletedProgram],
            userExercises: [todayLeftover]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.CreateProgramAsync(
            doctor.Id, patient.Id, DailyRequest(Today, Tomorrow, exercise.ExerciseId));

        // Assert
        result.Succeeded.Should().BeTrue();

        var adopted = await dbContext.Object.UserExercises
            .SingleAsync(ue => ue.UserExerciseId == todayLeftover.UserExerciseId);
        adopted.ProgramId.Should().Be(result.Value!.ProgramId);
        adopted.IsActive.Should().BeTrue();

        dbContext.Object.UserExercises
            .Where(ue => ue.ProgramId == result.Value.ProgramId)
            .Select(ue => ue.ScheduledDate)
            .Should().BeEquivalentTo([Today, Tomorrow]);
    }

    [Fact]
    public async Task CreateProgramAsync_WhenCompletedLeftoverFromDeletedProgramExistsToday_CreatesFreshAssignment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(createdByDoctorId: doctor.Id);
        var deletedProgram = Program(doctor.Id, patient.Id, isEnabled: false);

        // Completed-today leftovers stay active after delete; recreating must not
        // inherit their completion as "done" on the new program.
        var todayLeftover = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);
        todayLeftover.ProgramId = deletedProgram.ProgramId;
        var completion = ExerciseCompletionBuilder.Create(
            todayLeftover.UserExerciseId,
            patient.Id,
            doctor.Id,
            exercise.ExerciseId,
            Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            exercisePrograms: [deletedProgram],
            userExercises: [todayLeftover],
            exerciseCompletions: [completion]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.CreateProgramAsync(
            doctor.Id, patient.Id, DailyRequest(Today, Tomorrow, exercise.ExerciseId));

        // Assert
        result.Succeeded.Should().BeTrue();

        var retired = await dbContext.Object.UserExercises
            .IgnoreQueryFilters()
            .SingleAsync(ue => ue.UserExerciseId == todayLeftover.UserExerciseId);
        retired.IsActive.Should().BeFalse();
        retired.IsEnabled.Should().BeFalse();

        var freshToday = await dbContext.Object.UserExercises
            .SingleAsync(ue =>
                ue.ProgramId == result.Value!.ProgramId
                && ue.ScheduledDate == Today);
        freshToday.UserExerciseId.Should().NotBe(todayLeftover.UserExerciseId);

        var stillCompletedOnlyOnOld = await dbContext.Object.ExerciseCompletions
            .Where(c => c.UserExerciseId == freshToday.UserExerciseId)
            .ToListAsync();
        stillCompletedOnlyOnOld.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProgramAsync_WhenOrphanAssignmentWithoutProgramExistsToday_AdoptsAssignment()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var exercise = ExerciseBuilder.Create(createdByDoctorId: doctor.Id);

        // One-off assignment created via the assign-exercises modal (no program).
        var orphan = AssignmentBuilder.Create(
            doctor.Id, patient.Id, exercise.ExerciseId, scheduledDate: Today);

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [exercise],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            userExercises: [orphan]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act
        var result = await sut.CreateProgramAsync(
            doctor.Id, patient.Id, DailyRequest(Today, Today, exercise.ExerciseId));

        // Assert
        result.Succeeded.Should().BeTrue();

        var adopted = dbContext.Object.UserExercises.Should().ContainSingle().Subject;
        adopted.UserExerciseId.Should().Be(orphan.UserExerciseId);
        adopted.ProgramId.Should().Be(result.Value!.ProgramId);
    }

    [Fact]
    public async Task CreateProgramAsync_WhenAnotherEnabledProgramCoversSameDate_MergesDosageIntoExistingSchedule()
    {
        // Arrange
        var doctor = ApplicationUserBuilder.Doctor();
        var patient = ApplicationUserBuilder.Patient();
        var neckStretch = ExerciseBuilder.Create(title: "Neck Stretch", createdByDoctorId: doctor.Id);
        var shoulderRotation = ExerciseBuilder.Create(
            title: "Shoulder Rotation", id: Guid.NewGuid(), createdByDoctorId: doctor.Id);
        var backExtension = ExerciseBuilder.Create(
            title: "Back Extension", id: Guid.NewGuid(), createdByDoctorId: doctor.Id);
        var activeProgram = Program(doctor.Id, patient.Id, isEnabled: true);

        var existingNeck = AssignmentBuilder.Create(
            doctor.Id, patient.Id, neckStretch.ExerciseId, scheduledDate: Today);
        existingNeck.ProgramId = activeProgram.ProgramId;
        existingNeck.Reps = "10";

        var existingShoulder = AssignmentBuilder.Create(
            doctor.Id, patient.Id, shoulderRotation.ExerciseId, scheduledDate: Today);
        existingShoulder.ProgramId = activeProgram.ProgramId;
        existingShoulder.Reps = "15";

        var dbContext = AppDbContextMockFactory.CreateMock(
            users: [doctor, patient],
            exercises: [neckStretch, shoulderRotation, backExtension],
            doctorPatients: [DoctorPatientBuilder.Create(doctor.Id, patient.Id)],
            exercisePrograms: [activeProgram],
            userExercises: [existingNeck, existingShoulder]);

        var sut = new DoctorPatientService(dbContext.Object);

        // Act — new program overlaps Monday: update Neck Stretch reps + add Back Extension
        var result = await sut.CreateProgramAsync(
            doctor.Id,
            patient.Id,
            DailyRequestWithItems(
                Today,
                Today,
                Item(neckStretch.ExerciseId, reps: "20"),
                Item(backExtension.ExerciseId, reps: "12")));

        // Assert
        result.Succeeded.Should().BeTrue();

        var schedule = dbContext.Object.UserExercises
            .Where(ue => ue.IsActive && ue.IsEnabled && ue.ScheduledDate == Today)
            .ToList();
        schedule.Should().HaveCount(3);

        var mergedNeck = schedule.Single(ue => ue.ExerciseId == neckStretch.ExerciseId);
        mergedNeck.UserExerciseId.Should().Be(existingNeck.UserExerciseId);
        mergedNeck.Reps.Should().Be("20");
        mergedNeck.ProgramId.Should().Be(result.Value!.ProgramId);

        schedule.Should().Contain(ue =>
            ue.ExerciseId == shoulderRotation.ExerciseId
            && ue.Reps == "15"
            && ue.ProgramId == activeProgram.ProgramId);

        schedule.Should().Contain(ue =>
            ue.ExerciseId == backExtension.ExerciseId
            && ue.Reps == "12"
            && ue.ProgramId == result.Value.ProgramId);
    }
}
