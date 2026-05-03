using AgentAssist.Api.Endpoints;
using AgentAssist.Api.Errors;
using AgentAssist.Api.Middleware;
using AgentAssist.Application.Configuration;
using AgentAssist.Application.DependencyInjection;
using AgentAssist.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddMockInfrastructure();

builder.Services
    .AddOptions<AgentAssistOptions>()
    .Bind(builder.Configuration.GetSection(AgentAssistOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.Mode is AgentAssistMode.Mock, "Phase A only supports Mock mode")
    .ValidateOnStart();

var app = builder.Build();

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
    .WithDescription("Returns healthy for Phase A mock infrastructure.");

app.MapAssistantEndpoints();

app.Run();

/// <summary>
/// Application entry point marker for integration tests.
/// </summary>
public partial class Program;
