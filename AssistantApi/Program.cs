using AssistantApi;
using AssistantApi.Application.Agents;
using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using AssistantApi.Extensions;
using AssistantApi.Infrastructure;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services
    .AddApplicationOptions(builder.Configuration, builder.Environment.ContentRootPath)
    .AddInfrastructure(builder.Configuration)
    .AddHostedService<IngestionWorker>()
    .AddAgents()
    .AddValidationFromAssembly()
    .AddApiHealthChecks()
    .AddJwtAuthentication(builder.Configuration)
    .AddApiRateLimiting()
    .AddObservability()
    .AddCors(options => options.AddPolicy("AllowVscodeWebview",
        p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()))
    .AddControllers();

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen();

var app = builder.Build();

// ── Agent registrations ────────────────────────────────────────────────────────
// To add a new agent: implement IAgent, register it as Scoped above, add a block here.
// No other files need to change.
var registry = app.Services.GetRequiredService<IAgentRegistry>();

registry.Register(new AgentRegistration
{
    Name        = "InstructionAgent",
    Description = "Retrieves relevant coding standards and instructions from the instruction-embeddings collection",
    Condition   = _ => true,
    Factory     = sp => sp.GetRequiredService<InstructionAgent>()
});

registry.Register(new AgentRegistration
{
    Name        = "RepositoryAgent",
    Description = "Retrieves relevant code chunks from indexed repositories when the intent requires codebase context",
    Condition   = ctx => RulesAgentRouter.RequiresRepositoryContext(ctx.Intent),
    Factory     = sp => sp.GetRequiredService<RepositoryAgent>()
});

registry.Register(new AgentRegistration
{
    Name        = "CodingAgent",
    Description = "Assembles the LLM prompt from retrieved context and generates the response",
    Condition   = _ => true,
    Factory     = sp => sp.GetRequiredService<CodingAgent>()
});
// ── End agent registrations ────────────────────────────────────────────────────

await app.InitialiseAsync();

app.UseApiMiddleware()
   .MapHealthEndpoints()
   .MapControllers();

app.Run();
