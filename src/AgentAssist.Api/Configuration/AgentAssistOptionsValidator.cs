using AgentAssist.Application.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgentAssist.Api.Configuration;

/// <summary>
/// Cross-cutting <see cref="IValidateOptions{TOptions}"/> for <see cref="AgentAssistOptions"/> that fails fast at startup when the combination of host environment and pilot flags would expose the header-based <c>X-Agent-*</c> identity surface to a Production deployment (see ADR-0010).
/// </summary>
internal sealed class AgentAssistOptionsValidator(IHostEnvironment environment) : IValidateOptions<AgentAssistOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentAssistOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (environment.IsProduction()
            && options.Mode == AgentAssistMode.DevCloud
            && options.AllowHeaderUserContext)
        {
            return ValidateOptionsResult.Fail(
                "AgentAssist.AllowHeaderUserContext must be 'false' in the Production environment when Mode=DevCloud. The header-based pilot user context is explicitly out of scope for Production deployments and must be replaced by an authentication-backed IUserContextProvider before this configuration is allowed (see ADR-0010).");
        }

        return ValidateOptionsResult.Success;
    }
}
