using FluentAssertions;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;

namespace Phisio.Tests.DomainModel;

public class DailyPatientFeedbackTests
{
    [Fact]
    public void Submit_WithValidScores_CreatesFeedback()
    {
        var context = CareContext.From(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var feedback = DailyPatientFeedback.Submit(
            context,
            new DateOnly(2026, 9, 2),
            improvementScore: 4,
            hardnessScore: 3,
            comment: "Better today");

        feedback.ImprovementScore.Should().Be(4);
        feedback.HardnessScore.Should().Be(3);
        feedback.Comment.Should().Be("Better today");
        feedback.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Submit_WithInvalidScore_Throws(int score)
    {
        var act = () => DailyPatientFeedback.Submit(
            CareContext.From(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new DateOnly(2026, 9, 2),
            score,
            3,
            null);

        act.Should().Throw<DomainException>();
    }
}
