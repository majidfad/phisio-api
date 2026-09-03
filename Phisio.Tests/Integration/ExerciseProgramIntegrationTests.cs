using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers.Doctor;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

/// <summary>
/// End-to-end coverage for Exercise Program lifecycle and UserExercise materialization
/// via DoctorPatientsController program endpoints.
///
/// Architecture notes (current implementation):
/// - Programs are doctor-owned (<see cref="ExerciseProgram"/>) with <see cref="ProgramExercise"/> rows.
/// - Schedule expands via <see cref="ExerciseProgramSchedule"/> and materializes into <see cref="UserExercise"/>
///   from today onward (past dates in the window are not materialized).
/// - Exercises must be doctor-owned (CreatedByDoctorId == doctor). Admin catalog exercises are rejected.
/// - Duplicate exercise IDs in a request collapse to last dosage wins.
/// - There is no patient or admin program management API; patients only see materialized assignments.
/// - There is no GET-by-program-id endpoint; details come from GetPatientPrograms / GetPatientOverview.
/// </summary>
public sealed class ExerciseProgramIntegrationTests
{
    // 1. Doctor creates an exercise program for a connected patient.
    [Fact]
    public async Task CreateProgram_WhenPatientIsConnected_CreatesProgramAndMaterializesAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            today,
            today.AddDays(2),
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId, sets: 4, reps: "12"));

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject;
        body.AssignedCount.Should().Be(3);

        host.DbContext.ChangeTracker.Clear();
        var program = await host.DbContext.ExercisePrograms.SingleAsync();
        program.DoctorId.Should().Be(scenario.Doctor.Id);
        program.PatientId.Should().Be(scenario.Patient.Id);
        program.StartDate.Should().Be(today);
        program.EndDate.Should().Be(today.AddDays(2));
        program.IsEnabled.Should().BeTrue();

        var programExercises = await host.DbContext.ProgramExercises.Where(pe => pe.IsEnabled).ToListAsync();
        programExercises.Should().ContainSingle();
        programExercises[0].Sets.Should().Be(4);
        programExercises[0].Reps.Should().Be("12");

        var assignments = await host.DbContext.UserExercises
            .Where(ue => ue.IsActive && ue.IsEnabled)
            .ToListAsync();
        assignments.Should().HaveCount(3);
        assignments.Should().OnlyContain(ue =>
            ue.ProgramId == program.ProgramId
            && ue.DoctorId == scenario.Doctor.Id
            && ue.PatientId == scenario.Patient.Id
            && ue.ExerciseId == scenario.DoctorExercise.ExerciseId
            && ue.Sets == 4
            && ue.Reps == "12");
        assignments.Select(ue => ue.ScheduledDate)
            .Should().BeEquivalentTo([today, today.AddDays(1), today.AddDays(2)]);
    }

    // 2. Doctor creates a program for a patient who is not connected.
    [Fact]
    public async Task CreateProgram_WhenPatientIsNotConnected_ReturnsNotFoundAndCreatesNothing()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            ExerciseManagementTestHelpers.Today,
            ExerciseManagementTestHelpers.Today.AddDays(1),
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId));

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.OtherPatient.Id,
            request);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await host.DbContext.ExercisePrograms.CountAsync()).Should().Be(0);
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    // 3. Invalid ExerciseIds / non-doctor-owned exercises.
    [Fact]
    public async Task CreateProgram_WhenExerciseIdsAreInvalid_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            ExerciseManagementTestHelpers.Today,
            ExerciseManagementTestHelpers.Today.AddDays(1),
            ExerciseManagementTestHelpers.ProgramItem(Guid.NewGuid()));

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await host.DbContext.ExercisePrograms.CountAsync()).Should().Be(0);
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateProgram_WhenExerciseIsAdminCatalogOnly_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            ExerciseManagementTestHelpers.Today,
            ExerciseManagementTestHelpers.Today.AddDays(1),
            ExerciseManagementTestHelpers.ProgramItem(scenario.AdminExercise.ExerciseId));

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await host.DbContext.ExercisePrograms.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateProgram_WhenFluentValidationFails_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            today.AddDays(5),
            today,
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId));

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.ExercisePrograms.CountAsync()).Should().Be(0);
    }

    // 4. Duplicate exercises in request — last dosage wins; one ProgramExercise / one row per date.
    [Fact]
    public async Task CreateProgram_WhenDuplicateExerciseIdsProvided_UsesLastDosageAndDoesNotDuplicateRows()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            today,
            today,
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId, sets: 2, reps: "8"),
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId, sets: 5, reps: "15"));

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<CreateExerciseProgramResultDto>()
            .Which.AssignedCount.Should().Be(1);

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ProgramExercises.CountAsync(pe => pe.IsEnabled)).Should().Be(1);
        var assignment = await host.DbContext.UserExercises.SingleAsync();
        assignment.Sets.Should().Be(5);
        assignment.Reps.Should().Be("15");
    }

    // 5. Schedule materialization fields.
    [Fact]
    public async Task CreateProgram_PersistsDosageAndSchedulingFieldsOnUserExercises()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            today,
            today.AddDays(1),
            ExerciseManagementTestHelpers.ProgramItem(
                scenario.DoctorExercise.ExerciseId,
                sets: 3,
                reps: "10x",
                clinicianNote: "Slow tempo",
                patientCue: "Exhale"));

        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        host.DbContext.ChangeTracker.Clear();
        var assignments = await host.DbContext.UserExercises.ToListAsync();
        assignments.Should().HaveCount(2);
        assignments.Should().OnlyContain(ue =>
            ue.Sets == 3
            && ue.Reps == "10x"
            && ue.ClinicianNote == "Slow tempo"
            && ue.PatientCue == "Exhale"
            && ue.ProgramId != null
            && ue.IsActive
            && ue.IsEnabled);
    }

    // 6. Program spanning multiple days (interval cadence).
    [Fact]
    public async Task CreateProgram_WithIntervalCadence_CreatesExpectedDatesOnly()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;
        var request = ExerciseManagementTestHelpers.IntervalProgramRequest(
            today,
            today.AddDays(6),
            intervalDays: 2,
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId),
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise2.ExerciseId));

        var expectedDates = ExerciseManagementTestHelpers.ExpectedScheduleDates(request);
        expectedDates.Should().BeEquivalentTo(
            [today, today.AddDays(2), today.AddDays(4), today.AddDays(6)]);

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<CreateExerciseProgramResultDto>()
            .Which.AssignedCount.Should().Be(expectedDates.Count * 2);

        host.DbContext.ChangeTracker.Clear();
        var assignments = await host.DbContext.UserExercises.ToListAsync();
        assignments.Should().HaveCount(expectedDates.Count * 2);
        assignments.Select(ue => ue.ScheduledDate).Distinct()
            .Should().BeEquivalentTo(expectedDates);
        assignments.Should().NotContain(ue => !expectedDates.Contains(ue.ScheduledDate));
    }

    [Fact]
    public async Task CreateProgram_WhenStartDateIsInPast_OnlyMaterializesFromToday()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            today.AddDays(-2),
            today.AddDays(1),
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId));

        var result = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<CreateExerciseProgramResultDto>()
            .Which.AssignedCount.Should().Be(2);

        host.DbContext.ChangeTracker.Clear();
        var dates = await host.DbContext.UserExercises.Select(ue => ue.ScheduledDate).ToListAsync();
        dates.Should().BeEquivalentTo([today, today.AddDays(1)]);
        dates.Should().NotContain(today.AddDays(-1));
        dates.Should().NotContain(today.AddDays(-2));
    }

    // 7. Doctor updates an existing program.
    [Fact]
    public async Task UpdateProgram_WhenProgramExists_UpdatesProgramAndRegeneratesFutureAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var createResult = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(2),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId, sets: 2, reps: "8")));
        var programId = createResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        var updateRequest = ExerciseManagementTestHelpers.ToUpdateRequest(
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise2.ExerciseId, sets: 4, reps: "12")));

        var updateResult = await ExerciseManagementTestHelpers.UpdateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            programId,
            updateRequest);

        updateResult.Should().BeOfType<OkObjectResult>();

        host.DbContext.ChangeTracker.Clear();
        var program = await host.DbContext.ExercisePrograms.SingleAsync(p => p.ProgramId == programId);
        program.EndDate.Should().Be(today.AddDays(1));
        program.CadenceType.Should().Be(ExerciseProgramCadenceType.DaysOfWeek);

        var activeProgramExercises = await host.DbContext.ProgramExercises
            .Where(pe => pe.ProgramId == programId && pe.IsEnabled)
            .ToListAsync();
        activeProgramExercises.Should().ContainSingle()
            .Which.ExerciseId.Should().Be(scenario.DoctorExercise2.ExerciseId);

        var activeAssignments = await host.DbContext.UserExercises
            .Where(ue => ue.ProgramId == programId && ue.IsActive && ue.IsEnabled)
            .ToListAsync();
        activeAssignments.Should().HaveCount(2);
        activeAssignments.Should().OnlyContain(ue =>
            ue.ExerciseId == scenario.DoctorExercise2.ExerciseId
            && ue.Sets == 4
            && ue.Reps == "12");
        activeAssignments.Select(ue => ue.ScheduledDate)
            .Should().BeEquivalentTo([today, today.AddDays(1)]);

        // Previous future rows for the old exercise are retired.
        var retired = await host.DbContext.UserExercises
            .IgnoreQueryFilters()
            .Where(ue =>
                ue.ExerciseId == scenario.DoctorExercise.ExerciseId
                && (!ue.IsActive || !ue.IsEnabled))
            .ToListAsync();
        retired.Should().NotBeEmpty();
    }

    // 8. Update non-existing program.
    [Fact]
    public async Task UpdateProgram_WhenProgramDoesNotExist_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var result = await ExerciseManagementTestHelpers.UpdateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            Guid.NewGuid(),
            ExerciseManagementTestHelpers.ToUpdateRequest(
                ExerciseManagementTestHelpers.DailyProgramRequest(
                    today,
                    today,
                    ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId))));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await host.DbContext.ExercisePrograms.CountAsync()).Should().Be(0);
        (await host.DbContext.UserExercises.CountAsync()).Should().Be(0);
    }

    // 9. Doctor deletes a program.
    [Fact]
    public async Task DeleteProgram_WhenProgramExists_SoftDeletesProgramAndRetiresFutureAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var createResult = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(2),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)));
        var programId = createResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        var deleteResult = await controller.DeletePatientProgram(
            scenario.Patient.Id,
            programId,
            DoctorPatientBuilder.DefaultClinicId,
            CancellationToken.None);

        deleteResult.Should().BeOfType<NoContentResult>();

        host.DbContext.ChangeTracker.Clear();
        var program = await host.DbContext.ExercisePrograms
            .IgnoreQueryFilters()
            .SingleAsync(p => p.ProgramId == programId);
        program.IsEnabled.Should().BeFalse();

        var programExercises = await host.DbContext.ProgramExercises
            .IgnoreQueryFilters()
            .Where(pe => pe.ProgramId == programId)
            .ToListAsync();
        programExercises.Should().OnlyContain(pe => !pe.IsEnabled);

        var futureAssignments = await host.DbContext.UserExercises
            .IgnoreQueryFilters()
            .Where(ue => ue.ProgramId == programId && ue.ScheduledDate >= today)
            .ToListAsync();
        futureAssignments.Should().NotBeEmpty();
        futureAssignments.Should().OnlyContain(ue => !ue.IsActive && !ue.IsEnabled);
    }

    // 10. Delete non-existing program.
    [Fact]
    public async Task DeleteProgram_WhenProgramDoesNotExist_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        var result = await controller.DeletePatientProgram(
            scenario.Patient.Id,
            Guid.NewGuid(),
            DoctorPatientBuilder.DefaultClinicId,
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // 11. Program list.
    [Fact]
    public async Task GetPatientPrograms_ReturnsOnlyProgramsForConnectedPatientOwnedByDoctor()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today,
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)));
        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise2.ExerciseId)));

        var result = await controller.GetPatientPrograms(scenario.Patient.Id, DoctorPatientBuilder.DefaultClinicId, CancellationToken.None);

        var programs = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<ExerciseProgramDto>>().Subject;
        programs.Should().HaveCount(2);
        programs.Should().OnlyContain(p => p.PatientId == scenario.Patient.Id);
    }

    [Fact]
    public async Task GetPatientPrograms_WhenPatientNotConnected_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);

        var result = await controller.GetPatientPrograms(scenario.OtherPatient.Id, DoctorPatientBuilder.DefaultClinicId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // 12. Program details via list (no dedicated GET-by-id endpoint).
    [Fact]
    public async Task GetPatientPrograms_ReturnsExercisesScheduleAndAssignmentCounts()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(
                    scenario.DoctorExercise.ExerciseId,
                    sets: 3,
                    reps: "10",
                    clinicianNote: "Note",
                    patientCue: "Cue")));

        var result = await controller.GetPatientPrograms(scenario.Patient.Id, DoctorPatientBuilder.DefaultClinicId, CancellationToken.None);
        var programs = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IReadOnlyList<ExerciseProgramDto>>().Subject;
        var program = programs.Should().ContainSingle().Subject;

        program.StartDate.Should().Be(today);
        program.EndDate.Should().Be(today.AddDays(1));
        program.CadenceType.Should().Be(ExerciseProgramCadenceType.DaysOfWeek);
        program.DaysOfWeekMask.Should().Be(ExerciseManagementTestHelpers.EveryDayMask);
        program.UpcomingAssignmentCount.Should().Be(2);
        program.PastAssignmentCount.Should().Be(0);
        program.Exercises.Should().ContainSingle();
        program.Exercises[0].ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);
        program.Exercises[0].ExerciseName.Should().Be(scenario.DoctorExercise.Title);
        program.Exercises[0].Sets.Should().Be(3);
        program.Exercises[0].Reps.Should().Be("10");
        program.Exercises[0].ClinicianNote.Should().Be("Note");
        program.Exercises[0].PatientCue.Should().Be("Cue");
    }

    // 13. Overview / statistics.
    [Fact]
    public async Task GetPatientOverview_IncludesProgramsAndActiveTodayCountMatchingDatabase()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise2.ExerciseId)));

        var result = await controller.GetPatientOverview(scenario.Patient.Id, DoctorPatientBuilder.DefaultClinicId, CancellationToken.None);
        var overview = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DoctorPatientOverviewDto>().Subject;

        overview.PatientId.Should().Be(scenario.Patient.Id);
        overview.PatientName.Should().Be(scenario.Patient.Name);
        overview.Programs.Should().ContainSingle();
        overview.ActiveExerciseCountToday.Should().Be(2);

        host.DbContext.ChangeTracker.Clear();
        var todayCount = await host.DbContext.UserExercises.CountAsync(ue =>
            ue.DoctorId == scenario.Doctor.Id
            && ue.PatientId == scenario.Patient.Id
            && ue.ScheduledDate == today
            && ue.IsActive
            && ue.IsEnabled);
        overview.ActiveExerciseCountToday.Should().Be(todayCount);
    }

    [Fact]
    public async Task GetPatientExerciseStats_WhenProgramAssignmentsExist_ReturnsOk()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today,
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)));

        var result = await controller.GetPatientExerciseStats(
            scenario.Patient.Id,
            DoctorPatientBuilder.DefaultClinicId,
            from: today,
            to: today,
            cancellationToken: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    // 14. Doctor ownership rules.
    [Fact]
    public async Task ProgramEndpoints_WhenOtherDoctorActs_RejectAndLeaveDataUnchanged()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var ownerController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var createResult = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            ownerController,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)));
        var programId = createResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        var otherController = host.CreateDoctorPatientsController(scenario.OtherDoctor.Id);

        (await otherController.GetPatientPrograms(scenario.Patient.Id, DoctorPatientBuilder.DefaultClinicId, CancellationToken.None))
            .Should().BeOfType<NotFoundObjectResult>();

        (await ExerciseManagementTestHelpers.UpdateProgramWithValidationAsync(
                otherController,
                scenario.Patient.Id,
                programId,
                ExerciseManagementTestHelpers.ToUpdateRequest(
                    ExerciseManagementTestHelpers.DailyProgramRequest(
                        today,
                        today,
                        ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)))))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        (await otherController.DeletePatientProgram(
                scenario.Patient.Id,
                programId,
                DoctorPatientBuilder.DefaultClinicId,
                CancellationToken.None))
            .Should().BeOfType<NotFoundObjectResult>();

        (await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
                otherController,
                scenario.Patient.Id,
                ExerciseManagementTestHelpers.DailyProgramRequest(
                    today,
                    today,
                    ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId))))
            .Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ExercisePrograms.CountAsync(p => p.IsEnabled)).Should().Be(1);
        (await host.DbContext.UserExercises.CountAsync(ue => ue.IsActive)).Should().Be(2);
    }

    // 15. Patient visibility — no patient program API; patients see materialized assignments only.
    [Fact]
    public async Task PatientVisibility_PatientsHaveNoProgramEndpoints_AndSeeOnlyOwnAssignments()
    {
        typeof(DoctorPatientsController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.DoctorOnly);

        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var doctorController = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            doctorController,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today,
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)));

        await ExerciseManagementTestHostSeeder.SeedDoctorPatientLinkAsync(
            host,
            scenario.Doctor.Id,
            scenario.OtherPatient.Id,
            scenario.ClinicAId);
        var otherExercise = ExerciseBuilder.Create(
            title: "Other Patient Stretch",
            createdByDoctorId: scenario.Doctor.Id);
        host.DbContext.Exercises.Add(otherExercise);
        await host.DbContext.SaveChangesAsync();

        await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            doctorController,
            scenario.OtherPatient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today,
                ExerciseManagementTestHelpers.ProgramItem(otherExercise.ExerciseId)));

        var patientController = host.CreatePatientExercisesController(scenario.Patient.Id);
        var patientResult = await patientController.GetExercises(
            scheduledDate: today,
            doctorId: null,
            clinicId: null,
            cancellationToken: CancellationToken.None);

        patientResult.Should().BeOfType<OkObjectResult>();
        host.DbContext.ChangeTracker.Clear();
        var patientAssignments = await host.DbContext.UserExercises
            .Where(ue => ue.PatientId == scenario.Patient.Id && ue.IsActive)
            .ToListAsync();
        patientAssignments.Should().ContainSingle();
        patientAssignments[0].PatientId.Should().NotBe(scenario.OtherPatient.Id);
    }

    // 16. Authorization.
    [Fact]
    public async Task Authorization_DoctorOnlyPolicy_RejectsAnonymousPatientAndAdminWithoutDoctorRole()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DoctorOnly, policy =>
                policy.RequireRole(RoleNames.Doctor));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var patient = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Patient)],
            authenticationType: "Test"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Admin)],
            authenticationType: "Test"));
        var doctor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Doctor)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            admin, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.DoctorOnly))
            .Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task CreateProgram_WhenUserIdMissing_ReturnsUnauthorized()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var controller = host.CreateDoctorPatientsController(userId: null, RoleNames.Doctor);
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            ExerciseManagementTestHelpers.Today,
            ExerciseManagementTestHelpers.Today,
            ExerciseManagementTestHelpers.ProgramItem(Guid.NewGuid()));

        var result = await controller.CreatePatientProgram(
            Guid.NewGuid(),
            request,
            DoctorPatientBuilder.DefaultClinicId,
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // 17. Data integrity.
    [Fact]
    public async Task CreateProgram_PersistsForeignKeysAndDoesNotCreateOrphans()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var createResult = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today,
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise2.ExerciseId)));
        var programId = createResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        host.DbContext.ChangeTracker.Clear();
        var program = await host.DbContext.ExercisePrograms.SingleAsync();
        program.ProgramId.Should().Be(programId);
        program.DoctorId.Should().Be(scenario.Doctor.Id);
        program.PatientId.Should().Be(scenario.Patient.Id);

        var programExercises = await host.DbContext.ProgramExercises.Where(pe => pe.IsEnabled).ToListAsync();
        programExercises.Should().HaveCount(2);
        programExercises.Should().OnlyContain(pe => pe.ProgramId == programId);

        var assignments = await host.DbContext.UserExercises.ToListAsync();
        assignments.Should().HaveCount(2);
        assignments.Should().OnlyContain(ue =>
            ue.ProgramId == programId
            && ue.DoctorId == scenario.Doctor.Id
            && ue.PatientId == scenario.Patient.Id);

        var entityType = host.DbContext.Model.FindEntityType(typeof(ExerciseProgram));
        entityType.Should().NotBeNull();
        var foreignKeys = entityType!.GetForeignKeys()
            .SelectMany(fk => fk.Properties.Select(p => p.Name))
            .ToList();
        foreignKeys.Should().Contain("DoctorId");
        foreignKeys.Should().Contain("PatientId");
    }

    // 18. Duplicate program creation — second program is allowed; assignments merge (no duplicate day rows).
    [Fact]
    public async Task CreateProgram_WhenSubmittedTwice_CreatesSecondProgramAndMergesAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;
        var request = ExerciseManagementTestHelpers.DailyProgramRequest(
            today,
            today.AddDays(1),
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId, sets: 2, reps: "8"));

        var first = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            request);
        var firstId = first.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        var secondRequest = ExerciseManagementTestHelpers.DailyProgramRequest(
            today,
            today.AddDays(1),
            ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId, sets: 6, reps: "20"));
        var second = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            secondRequest);
        var secondBody = second.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject;

        // Merge path updates existing rows in place (AssignedCount counts only new/reactivated rows).
        secondBody.ProgramId.Should().NotBe(firstId);

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ExercisePrograms.CountAsync(p => p.IsEnabled)).Should().Be(2);

        var assignments = await host.DbContext.UserExercises
            .Where(ue => ue.IsActive && ue.IsEnabled)
            .ToListAsync();
        assignments.Should().HaveCount(2);
        assignments.Should().OnlyContain(ue =>
            ue.ProgramId == secondBody.ProgramId
            && ue.Sets == 6
            && ue.Reps == "20");
    }

    // 19. Save failure rollback on create.
    [Fact]
    public async Task CreateProgram_WhenSaveFails_LeavesNoPartialProgramOrAssignments()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync(services =>
            services.UseFailingProgramSaveInterceptor());
        var interceptor = host.GetRequiredService<FailingProgramSaveInterceptor>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        interceptor.FailOnNextProgramRelatedSave = true;

        var act = async () => await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                ExerciseManagementTestHelpers.Today,
                ExerciseManagementTestHelpers.Today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated program persistence failure.");

        host.DbContext.ChangeTracker.Clear();
        (await host.DbContext.ExercisePrograms.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.ProgramExercises.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await host.DbContext.UserExercises.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // 20. Save failure during update/delete.
    [Fact]
    public async Task UpdateProgram_WhenSaveFails_LeavesOriginalProgramAndAssignmentsIntact()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync(services =>
            services.UseFailingProgramSaveInterceptor());
        var interceptor = host.GetRequiredService<FailingProgramSaveInterceptor>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var createResult = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId, sets: 2, reps: "8")));
        var programId = createResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        host.DbContext.ChangeTracker.Clear();
        interceptor.FailOnNextProgramRelatedSave = true;

        var act = async () => await ExerciseManagementTestHelpers.UpdateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            programId,
            ExerciseManagementTestHelpers.ToUpdateRequest(
                ExerciseManagementTestHelpers.DailyProgramRequest(
                    today,
                    today,
                    ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise2.ExerciseId, sets: 9, reps: "99"))));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated program persistence failure.");

        host.DbContext.ChangeTracker.Clear();
        var program = await host.DbContext.ExercisePrograms.SingleAsync(p => p.ProgramId == programId);
        program.EndDate.Should().Be(today.AddDays(1));
        program.IsEnabled.Should().BeTrue();

        var activeExercises = await host.DbContext.ProgramExercises
            .Where(pe => pe.ProgramId == programId && pe.IsEnabled)
            .ToListAsync();
        activeExercises.Should().ContainSingle()
            .Which.ExerciseId.Should().Be(scenario.DoctorExercise.ExerciseId);

        var assignments = await host.DbContext.UserExercises
            .Where(ue => ue.ProgramId == programId && ue.IsActive && ue.IsEnabled)
            .ToListAsync();
        assignments.Should().HaveCount(2);
        assignments.Should().OnlyContain(ue =>
            ue.ExerciseId == scenario.DoctorExercise.ExerciseId
            && ue.Sets == 2
            && ue.Reps == "8");
    }

    [Fact]
    public async Task DeleteProgram_WhenSaveFails_LeavesProgramEnabled()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync(services =>
            services.UseFailingProgramSaveInterceptor());
        var interceptor = host.GetRequiredService<FailingProgramSaveInterceptor>();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateDoctorPatientsController(scenario.Doctor.Id);
        var today = ExerciseManagementTestHelpers.Today;

        var createResult = await ExerciseManagementTestHelpers.CreateProgramWithValidationAsync(
            controller,
            scenario.Patient.Id,
            ExerciseManagementTestHelpers.DailyProgramRequest(
                today,
                today.AddDays(1),
                ExerciseManagementTestHelpers.ProgramItem(scenario.DoctorExercise.ExerciseId)));
        var programId = createResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<CreateExerciseProgramResultDto>().Subject.ProgramId;

        host.DbContext.ChangeTracker.Clear();
        interceptor.FailOnNextProgramRelatedSave = true;

        var act = async () => await controller.DeletePatientProgram(
            scenario.Patient.Id,
            programId,
            DoctorPatientBuilder.DefaultClinicId,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated program persistence failure.");

        host.DbContext.ChangeTracker.Clear();
        var program = await host.DbContext.ExercisePrograms.SingleAsync(p => p.ProgramId == programId);
        program.IsEnabled.Should().BeTrue();
        (await host.DbContext.UserExercises.CountAsync(ue =>
                ue.ProgramId == programId && ue.IsActive && ue.IsEnabled))
            .Should().Be(2);
    }
}
