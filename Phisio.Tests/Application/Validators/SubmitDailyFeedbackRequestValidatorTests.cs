using FluentValidation.TestHelper;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Application.PatientDailyFeedback.Validators;

namespace Phisio.Tests.Application.Validators;

public class SubmitDailyFeedbackRequestValidatorTests
{
    private readonly SubmitDailyFeedbackRequestValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Validate_WhenImprovementScoreIsOutOfRange_ReturnsError(int score)
    {
        // Arrange
        var request = new SubmitDailyFeedbackRequest { ImprovementScore = score };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ImprovementScore);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Validate_WhenImprovementScoreIsValid_ReturnsNoErrors(int score)
    {
        // Arrange
        var request = new SubmitDailyFeedbackRequest { ImprovementScore = score };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.ImprovementScore);
    }

    [Fact]
    public void Validate_WhenCommentExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var request = new SubmitDailyFeedbackRequest
        {
            ImprovementScore = 4,
            Comment = new string('ا', 1001),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Comment);
    }
}
