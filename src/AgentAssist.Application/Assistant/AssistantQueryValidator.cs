using AgentAssist.Domain;
using FluentValidation;

namespace AgentAssist.Application.Assistant;

/// <summary>
/// Validates the assistant query domain rules (question presence, length, and at least one role).
/// </summary>
public sealed class AssistantQueryValidator : AbstractValidator<AssistantQuery>
{
    /// <summary>
    /// The maximum allowed question length, aligned with the API request contract.
    /// </summary>
    public const int MaxQuestionLength = 2000;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssistantQueryValidator"/> class.
    /// </summary>
    public AssistantQueryValidator()
    {
        RuleFor(query => query.Question)
            .NotEmpty()
            .WithMessage("Question is required.")
            .MaximumLength(MaxQuestionLength)
            .WithMessage($"Question must be at most {MaxQuestionLength} characters.");

        RuleFor(query => query.Roles)
            .NotEmpty()
            .WithMessage("At least one role is required.");
    }
}
