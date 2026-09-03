using Microsoft.EntityFrameworkCore;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services.Care;

internal static class PatientAssignmentMaterializer
{
    internal static void ApplyDosage(
        UserExercise assignment,
        AssignPatientExerciseItem dosage,
        DateTime assignedAt) =>
        assignment.ApplyLatestDosage(
            assignedAt,
            dosage.Sets,
            dosage.Reps,
            dosage.ClinicianNote,
            dosage.PatientCue);

    /// <summary>
    /// Materializes program dates into the patient schedule by merging:
    /// same exercise+date updates dosage (latest wins); different exercises coexist.
    /// Completed leftovers are retired so a fresh row is created.
    /// </summary>
    internal static async Task<int> MaterializeProgramAssignmentsAsync(
        AppDbContext dbContext,
        ExerciseProgram program,
        IReadOnlyList<DateOnly> scheduleDates,
        IReadOnlyList<Guid> validExerciseIds,
        IReadOnlyDictionary<Guid, AssignPatientExerciseItem> itemsByExerciseId,
        CancellationToken cancellationToken)
    {
        if (scheduleDates.Count == 0 || validExerciseIds.Count == 0)
        {
            return 0;
        }

        var doctorId = program.DoctorId;
        var patientId = program.PatientId;
        var clinicId = program.ClinicId;

        var existingActive = await dbContext.UserExercises
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && assignment.ClinicId == clinicId
                && assignment.IsActive
                && assignment.IsEnabled
                && validExerciseIds.Contains(assignment.ExerciseId)
                && scheduleDates.Contains(assignment.ScheduledDate))
            .ToListAsync(cancellationToken);

        var inactiveAssignments = await dbContext.UserExercises
            .IgnoreQueryFilters()
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && assignment.ClinicId == clinicId
                && (!assignment.IsActive || !assignment.IsEnabled)
                && validExerciseIds.Contains(assignment.ExerciseId)
                && scheduleDates.Contains(assignment.ScheduledDate))
            .ToListAsync(cancellationToken);

        var candidateIds = existingActive
            .Concat(inactiveAssignments)
            .Select(assignment => assignment.UserExerciseId)
            .Distinct()
            .ToList();
        var completedAssignmentIds = candidateIds.Count == 0
            ? []
            : (await dbContext.ExerciseCompletions
                .AsNoTracking()
                .Where(completion =>
                    completion.IsEnabled
                    && candidateIds.Contains(completion.UserExerciseId)
                    && scheduleDates.Contains(completion.CompletionDate))
                .Select(completion => new { completion.UserExerciseId, completion.CompletionDate })
                .ToListAsync(cancellationToken))
                .Where(completion => existingActive
                        .Concat(inactiveAssignments)
                        .Any(assignment =>
                            assignment.UserExerciseId == completion.UserExerciseId
                            && assignment.ScheduledDate == completion.CompletionDate))
                .Select(completion => completion.UserExerciseId)
                .ToHashSet();

        foreach (var completed in existingActive.Where(a => completedAssignmentIds.Contains(a.UserExerciseId)))
        {
            completed.Retire();
        }

        var existingByKey = existingActive
            .Where(assignment => !completedAssignmentIds.Contains(assignment.UserExerciseId))
            .GroupBy(assignment => (assignment.ExerciseId, assignment.ScheduledDate))
            .ToDictionary(group => group.Key, group => group.First());

        var inactiveByKey = inactiveAssignments
            .Where(assignment => !completedAssignmentIds.Contains(assignment.UserExerciseId))
            .GroupBy(assignment => (assignment.ExerciseId, assignment.ScheduledDate))
            .ToDictionary(group => group.Key, group => group.First());

        var assignedAt = DateTime.UtcNow;
        var assignedCount = 0;

        foreach (var scheduledDate in scheduleDates)
        {
            foreach (var exerciseId in validExerciseIds)
            {
                var key = (exerciseId, scheduledDate);
                var dosage = itemsByExerciseId[exerciseId];

                if (existingByKey.TryGetValue(key, out var existingAssignment))
                {
                    existingAssignment.LinkToProgram(program);
                    ApplyDosage(existingAssignment, dosage, assignedAt);
                    continue;
                }

                if (inactiveByKey.TryGetValue(key, out var inactiveAssignment))
                {
                    inactiveAssignment.Reactivate(assignedAt, program.ProgramId);
                    inactiveAssignment.LinkToProgram(program);
                    ApplyDosage(inactiveAssignment, dosage, assignedAt);
                }
                else
                {
                    var assignment = UserExercise.CreateFromProgram(
                        program,
                        exerciseId,
                        scheduledDate,
                        assignedAt);
                    ApplyDosage(assignment, dosage, assignedAt);
                    dbContext.UserExercises.Add(assignment);
                }

                assignedCount++;
            }
        }

        return assignedCount;
    }
}
