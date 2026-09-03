using FluentAssertions;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;

namespace Phisio.Tests.DomainModel;

public class CareContextTests
{
    [Fact]
    public void EnsureValid_WhenClinicIdEmpty_ThrowsDomainException()
    {
        var context = new CareContext(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
        var act = () => context.EnsureValid();
        act.Should().Throw<DomainException>().WithMessage("*ClinicId*");
    }

    [Fact]
    public void Matches_WhenAllIdsEqual_ReturnsTrue()
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var context = CareContext.From(doctorId, patientId, clinicId);
        context.Matches(doctorId, patientId, clinicId).Should().BeTrue();
    }
}

public class UserExerciseTests
{
    [Fact]
    public void CreateFromProgram_WhenClinicMatches_SetsProgramId()
    {
        var program = new ExerciseProgram
        {
            ProgramId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ClinicId = Guid.NewGuid(),
        };

        var assignment = UserExercise.CreateFromProgram(
            program,
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTime.UtcNow);

        assignment.ProgramId.Should().Be(program.ProgramId);
        assignment.ClinicId.Should().Be(program.ClinicId);
    }

    [Fact]
    public void LinkToProgram_WhenClinicMismatch_ThrowsDomainException()
    {
        var assignment = UserExercise.CreateAdHoc(
            CareContext.From(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTime.UtcNow);

        var otherProgram = new ExerciseProgram
        {
            ProgramId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ClinicId = Guid.NewGuid(),
        };

        var act = () => assignment.LinkToProgram(otherProgram);
        act.Should().Throw<DomainException>();
    }
}
