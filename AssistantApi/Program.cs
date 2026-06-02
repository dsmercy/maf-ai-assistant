using AssistantApi.Application.Agents;
using AssistantApi.Application.Configuration;
using AssistantApi.Application.Services;
using AssistantApi.Application.Validators;
using AssistantApi.HealthChecks;
using AssistantApi.Infrastructure;
using AssistantApi.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Options
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection(AssistantOptions.SectionName));

// Infrastructure (EF, Ollama, Qdrant)
builder.Services.AddInfrastructure(builder.Configuration);

// Agents
builder.Services.AddScoped<RepositoryAgent>();
builder.Services.AddScoped<InstructionAgent>();
builder.Services.AddScoped<CodingAgent>();
builder.Services.AddScoped<OrchestratorAgent>();
builder.Services.AddScoped<ChatService>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<OllamaHealthCheck>("ollama", tags: ["ready"])
    .AddCheck<QdrantHealthCheck>("qdrant", tags: ["ready"])
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

// JWT — validation wired in Phase 5, middleware registered now
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "replace_with_32_char_secret_minimum";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev only
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,  // enforced Phase 5
            ValidateAudience = false,
            ValidateLifetime = true
        };
        // Return 401 instead of redirecting
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Unauthorized" }));
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-apply EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AssistantApi.Infrastructure.Persistence.AssistantDbContext>();
    db.Database.EnsureCreated();
}

app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Liveness — always returns 200
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// Readiness — checks all downstream services
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
});

app.MapControllers();

app.Run();
