using FluentValidation.TestHelper;
using SheikhTravelSystem.Application.Features.TripFeedback;

namespace SheikhTravelSystem.Tests.TripFeedback;

public class TripFeedbackValidatorTests
{
    [Fact]
    public void Submit_rejects_invalid_politeness()
    {
        var validator = new SubmitTripFeedbackCommandValidator();
        var result = validator.TestValidate(new SubmitTripFeedbackCommand(
            "token123456789012",
            new SubmitTripFeedbackRequest(0, 3, 3, null)));
        result.ShouldHaveValidationErrorFor(x => x.Body.Politeness);
    }

    [Fact]
    public void Submit_accepts_valid_scores()
    {
        var validator = new SubmitTripFeedbackCommandValidator();
        var result = validator.TestValidate(new SubmitTripFeedbackCommand(
            "token123456789012",
            new SubmitTripFeedbackRequest(5, 4, 3, "Great trip")));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
