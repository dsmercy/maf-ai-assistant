using AssistantApi;
using AssistantApi.Extensions;
using AssistantApi.Infrastructure;
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

await app.InitialiseAsync();

app.UseApiMiddleware()
   .MapHealthEndpoints()
   .MapControllers();

app.Run();
