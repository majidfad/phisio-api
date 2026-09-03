using FluentAssertions;
using Phisio.Domain.CarePlans;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Tests.DomainModel;

public class ExerciseProgramTests
{
    [Fact]
    public void Create_SetsCareContextAndSchedule()
    {
        var context = CareContext.From(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var program = ExerciseProgram.Create(
            context,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            ExerciseProgramCadenceType.DaysOfWeek,
            daysOfWeekMask: 0b1111111,
            intervalDays: null);

        program.DoctorId.Should().Be(context.DoctorId);
        program.PatientId.Should().Be(context.PatientId);
        program.ClinicId.Should().Be(context.ClinicId);
        program.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ReplaceExercises_DisablesPreviousAndCreatesNew()
    {
        var program = ExerciseProgram.Create(
            CareContext.From(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            ExerciseProgramCadenceType.DaysOfWeek,
            0b1111111,
            null,
            Guid.NewGuid());

        program.Exercises.Add(ProgramExercise.Create(program.ProgramId, Guid.NewGuid(), 3, "10", null, null));
        var exerciseId = Guid.NewGuid();

        var created = program.ReplaceExercises([
            new ProgramExerciseDosage(exerciseId, 4, "12", "Note", "Cue"),
        ]);

        program.Exercises.Should().ContainSingle(e => !e.IsEnabled);
        created.Should().ContainSingle();
        created[0].ExerciseId.Should().Be(exerciseId);
        created[0].Sets.Should().Be(4);
    }

    [Fact]
    public void SoftDeleteWithExercises_DisablesProgramAndExercises()
    {
        var program = ExerciseProgram.Create(
            CareContext.From(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            ExerciseProgramCadenceType.DaysOfWeek,
            0b1111111,
            null,
            Guid.NewGuid());

        program.Exercises.Add(ProgramExercise.Create(program.ProgramId, Guid.NewGuid(), 3, "10", null, null));

        program.SoftDeleteWithExercises();

        program.IsEnabled.Should().BeFalse();
        program.Exercises.Should().OnlyContain(e => !e.IsEnabled);
    }
}
