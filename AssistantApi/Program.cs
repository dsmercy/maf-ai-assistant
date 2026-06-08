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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Options
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection(AssistantOptions.SectionName));

// Resolve relative ingestion paths against the application's content root so that
// "data/uploads" becomes "<project-dir>/data/uploads" when running locally,
// while absolute Docker paths like "/data/uploads" are left unchanged.
// PostConfigure runs after all Configure calls (including AddInfrastructure) so it wins.
builder.Services.PostConfigure<AssistantApi.Infrastructure.Ingestion.IngestionOptions>(opts =>
{
    var root = builder.Environment.ContentRootPath;
    if (!Path.IsPathRooted(opts.UploadPath))
        opts.UploadPath = Path.GetFullPath(Path.Combine(root, opts.UploadPath));
    if (!Path.IsPathRooted(opts.RepositoryCachePath))
        opts.RepositoryCachePath = Path.GetFullPath(Path.Combine(root, opts.RepositoryCachePath));
});

// Infrastructure (EF, Ollama, Qdrant)
builder.Services.AddInfrastructure(builder.Configuration);

// Agents — order matters: leaf agents first, orchestrator last
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

// JWT — Open WebUI issues tokens; we validate them on all non-health endpoints
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "replace_with_32_char_secret_minimum";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev only — enable in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,  // Open WebUI doesn't set a fixed issuer
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                // OpenAI-format error so Open WebUI can parse it correctly
                return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = new
                    {
                        message = "Missing bearer authentication in header",
                        type = "invalid_request_error",
                        param = (string?)null,
                        code = (string?)null
                    }
                }));
            },
            OnMessageReceived = ctx =>
            {
                // Accept static API key (WEBUI_SECRET_KEY) as a valid bearer token for /v1/* routes
                // This allows Open WebUI to call our API without needing a full JWT
                var apiKey = builder.Configuration["Jwt:Secret"];
                var token = ctx.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token) && token == apiKey && ctx.Request.Path.StartsWithSegments("/v1"))
                {
                    // Synthesise a simple identity so the request passes auth
                    var claims = new[] { new System.Security.Claims.Claim("sub", "open-webui") };
                    var identity = new System.Security.Claims.ClaimsIdentity(claims, "ApiKey");
                    ctx.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                    ctx.Success();
                }
                return Task.CompletedTask;
            },
            // Extract user identity from Open WebUI JWT claims
            OnTokenValidated = ctx =>
            {
                var email = ctx.Principal?.FindFirst("email")?.Value
                         ?? ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                         ?? ctx.Principal?.FindFirst("sub")?.Value
                         ?? "anonymous";
                ctx.HttpContext.Items["UserId"] = email;
                return Task.CompletedTask;
            }
        };
    });

// TODO: Re-enable auth enforcement for production (Phase 5)
// builder.Services.AddAuthorization(options =>
// {
//     options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
//         .RequireAuthenticatedUser()
//         .Build();
// });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rate limiting — protect POST endpoints from abuse
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Chat: 20 requests per minute per IP
    options.AddFixedWindowLimiter("chat", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 20;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Ingestion: 10 uploads per minute per IP
    options.AddFixedWindowLimiter("ingestion", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// OpenTelemetry — distributed tracing and metrics
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("assistant-api"))
        .AddAspNetCoreInstrumentation(opts => opts.RecordException = true)
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())   // swap for OTLP exporter in production
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("assistant-api"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

// Auto-apply EF migrations on startup and seed default feature flags.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AssistantApi.Infrastructure.Persistence.AssistantDbContext>();
    db.Database.EnsureCreated();

    var flagRepo = scope.ServiceProvider.GetRequiredService<AssistantApi.Core.Interfaces.IFeatureFlagRepository>();
    await SeedFlagIfMissingAsync(flagRepo, "code-embeddings", isEnabled: false,
        "Source-code RAG — search code-embeddings collection. Disable when no repositories are indexed.");
    await SeedFlagIfMissingAsync(flagRepo, "doc-embeddings",  isEnabled: true,
        "Document RAG — search doc-embeddings collection for uploaded PDFs, DOCX, and Markdown.");
    await SeedFlagIfMissingAsync(flagRepo, "streaming",       isEnabled: true,
        "Enable token streaming on /api/chat/stream and /v1/chat/completions.");
    //await SeedFlagIfMissingAsync(flagRepo, "rag",             isEnabled: true,
    //    "Enable RAG retrieval via RepositoryAgent.");
    await SeedFlagIfMissingAsync(flagRepo, "auth",            isEnabled: false,
        "Enforce JWT authentication on all non-health endpoints.");
    await SeedFlagIfMissingAsync(flagRepo, "audit",           isEnabled: true,
        "Write all API requests to the audit_logs table.");
    await SeedFlagIfMissingAsync(flagRepo, "rate-limit",      isEnabled: true,
        "Apply rate limiting on POST /api/chat and ingestion endpoints.");

    var templateRepo = scope.ServiceProvider.GetRequiredService<AssistantApi.Core.Interfaces.IPromptTemplateRepository>();
    await SeedPromptTemplatesAsync(templateRepo);
}

static async Task SeedFlagIfMissingAsync(
    AssistantApi.Core.Interfaces.IFeatureFlagRepository repo,
    string name, bool isEnabled, string description)
{
    var all = await repo.GetAllAsync();
    if (all.Any(f => f.Name == name)) return;
    await repo.UpsertAsync(new AssistantApi.Core.Entities.FeatureFlag
    {
        Name        = name,
        IsEnabled   = isEnabled,
        Description = description
    });
}

static async Task SeedPromptTemplatesAsync(AssistantApi.Core.Interfaces.IPromptTemplateRepository repo)
{
    var defaults = new[]
    {
        new AssistantApi.Core.Entities.PromptTemplate
        {
            Name               = "Code Generation",
            TaskType           = "CodeGeneration",
            SystemPrompt       = "You are an expert software engineer. Generate clean, production-ready code.\nFollow these coding standards strictly:\n{instructions}\n\nUse the following context from documentation as reference:\n{context_chunks}",
            UserPromptTemplate = "Generate the following: {user_message}\n\nLanguage/Framework: {language}\nProvide complete, working code with no placeholders.",
            IsActive           = true
        },
        new AssistantApi.Core.Entities.PromptTemplate
        {
            Name               = "Code Review",
            TaskType           = "CodeReview",
            SystemPrompt       = "You are a senior code reviewer. Review code for correctness, maintainability, and standards compliance.\nCoding standards to enforce:\n{instructions}\n\nRelevant context:\n{context_chunks}",
            UserPromptTemplate = "Review the following: {user_message}\n\nIdentify: bugs, standards violations, improvements. Be specific and actionable.",
            IsActive           = true
        },
        new AssistantApi.Core.Entities.PromptTemplate
        {
            Name               = "Unit Test Generation",
            TaskType           = "UnitTest",
            SystemPrompt       = "You are an expert in software testing. Generate comprehensive unit tests.\nTesting standards:\n{instructions}\n\nCode under test:\n{context_chunks}",
            UserPromptTemplate = "Generate unit tests for: {user_message}\n\nLanguage: {language}\nInclude: arrange/act/assert, edge cases, meaningful test names.",
            IsActive           = true
        },
        new AssistantApi.Core.Entities.PromptTemplate
        {
            Name               = "Documentation",
            TaskType           = "Documentation",
            SystemPrompt       = "You are a technical writer and software engineer. Generate clear documentation.\nDocumentation standards:\n{instructions}\n\nCode context:\n{context_chunks}",
            UserPromptTemplate = "Generate documentation for: {user_message}\n\nInclude: purpose, parameters, return values, examples where appropriate.",
            IsActive           = true
        },
        new AssistantApi.Core.Entities.PromptTemplate
        {
            Name               = "Code Explanation",
            TaskType           = "CodeExplanation",
            SystemPrompt       = "You are an expert software engineer. Explain code clearly and concisely.\nCoding standards for reference:\n{instructions}\n\nRelevant context:\n{context_chunks}",
            UserPromptTemplate = "{user_message}\n\nExplain clearly. Reference specific lines or patterns where relevant.",
            IsActive           = true
        },
        new AssistantApi.Core.Entities.PromptTemplate
        {
            Name               = "Repository Question",
            TaskType           = "RepositoryQuestion",
            SystemPrompt       = "You are an expert software engineer with full knowledge of this codebase.\nCoding standards:\n{instructions}\n\nRelevant context from the repository:\n{context_chunks}",
            UserPromptTemplate = "{user_message}\n\nBase your answer on the provided context. Reference specific files and patterns.",
            IsActive           = true
        },
        new AssistantApi.Core.Entities.PromptTemplate
        {
            Name               = "General Question",
            TaskType           = "GeneralQuestion",
            SystemPrompt       = "You are an expert software engineering assistant.\n{instructions}",
            UserPromptTemplate = "{user_message}",
            IsActive           = true
        },
    };

    var existing = await repo.GetAllAsync();
    var existingTaskTypes = existing.Select(t => t.TaskType).ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var template in defaults.Where(t => !existingTaskTypes.Contains(t.TaskType)))
        await repo.UpsertAsync(template);
}

app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

// Liveness — always returns 200, no auth required
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

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
}).AllowAnonymous();

app.MapControllers();

app.Run();
