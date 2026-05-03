using AgentAssist.Domain;
using FluentValidation;

namespace AgentAssist.Application.Assistant;

/// <summary>
/// Validates cross-field assistant query rules.
/// </summary>
public sealed class AssistantQueryValidator : AbstractValidator<AssistantQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssistantQueryValidator"/> class.
    /// </summary>
    public AssistantQueryValidator()
    {
        RuleFor(query => query.Roles)
            .NotEmpty()
            .WithMessage("At least one role is required.");
    }
}
