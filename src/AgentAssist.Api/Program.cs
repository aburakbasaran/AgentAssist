using AgentAssist.Api.Configuration;
using AgentAssist.Api.Endpoints;
using AgentAssist.Api.Errors;
using AgentAssist.Api.Logging;
using AgentAssist.Api.Middleware;
using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Configuration;
using AgentAssist.Application.DependencyInjection;
using AgentAssist.Infrastructure.DependencyInjection;

using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    SensitiveDataRedactingLoggerFactory.RegisterDecorator(builder.Services);
}

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();

builder.Services
    .AddOptions<AgentAssistOptions>()
    .Bind(builder.Configuration.GetSection(AgentAssistOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<AgentAssistOptions>, AgentAssistOptionsValidator>();

var agentAssistSection = builder.Configuration.GetSection(AgentAssistOptions.SectionName);
var agentAssistMode = agentAssistSection.GetValue<AgentAssistMode>(nameof(AgentAssistOptions.Mode));
var allowHeaderUserContext = agentAssistSection.GetValue<bool>(nameof(AgentAssistOptions.AllowHeaderUserContext), defaultValue: true);

if (agentAssistMode is AgentAssistMode.DevCloud)
{
    builder.Services.AddDevCloudInfrastructure(builder.Configuration);
}
else
{
    builder.Services.AddMockInfrastructure();
}

var environment = builder.Environment;
var inPilotEnvironment = environment.IsDevelopment()
    || string.Equals(environment.EnvironmentName, "InternalPilot", StringComparison.Ordinal);

UserContextSource userContextSource;
if (inPilotEnvironment && allowHeaderUserContext)
{
    userContextSource = UserContextSource.Header;
}
else if (agentAssistMode is AgentAssistMode.Mock)
{
    userContextSource = UserContextSource.Mock;
}
else
{
    userContextSource = UserContextSource.None;
}

builder.Services.AddPilotUserContext(userContextSource);

var app = builder.Build();

if (userContextSource is not UserContextSource.Header)
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AgentAssist.Startup");
    startupLogger.LogWarning(
        "Header-based pilot user context is disabled in environment {Environment} with mode {Mode}. Active context source = {Source}. Add Entra ID or another authentication-backed provider before exposing this app to untrusted networks (see ADR-0010).",
        environment.EnvironmentName,
        agentAssistMode,
        userContextSource);
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live")
    .AllowAnonymous()
    .WithSummary("Liveness health check")
    .WithDescription("Returns healthy when the process is running.");

app.MapHealthChecks("/health/ready")
    .AllowAnonymous()
    .WithSummary("Readiness health check")
    .WithDescription("Aggregates dependency health checks (Search, OpenAI, SQL in DevCloud; lightweight self check in Mock).");

app.MapAssistantEndpoints();

app.Run();

/// <summary>
/// Application entry point marker for integration tests.
/// </summary>
public partial class Program;
