using AssistantApi.Core.Interfaces;
using AssistantApi.Infrastructure.Ingestion;
using AssistantApi.Infrastructure.Ingestion.Parsers;
using AssistantApi.Infrastructure.Ollama;
using AssistantApi.Infrastructure.Persistence;
using AssistantApi.Infrastructure.Qdrant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;

namespace AssistantApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL
        services.AddDbContext<AssistantDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IMetadataRepository, MetadataRepository>();
        services.AddScoped<IFileHashRepository, FileHashRepository>();
        services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<IDocumentTagRepository, DocumentTagRepository>();
        services.AddSingleton<ITagVocabularyCache, TagVocabularyCache>();

        // Ollama
        var ollamaOptions = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>()
                            ?? new OllamaOptions();
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
        {
            client.BaseAddress = new Uri(ollamaOptions.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
        }).AddStandardResilienceHandler(options =>
        {
            // LLM inference can take minutes — override the default 30s attempt timeout
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(8);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
            // Circuit breaker sampling must be >= 2× attempt timeout
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(17);
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
        });

        // Qdrant
        var qdrantHost = configuration["Qdrant:Host"] ?? "localhost";
        var qdrantPort = int.TryParse(configuration["Qdrant:Port"], out var p) ? p : 6334;
        services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));
        services.AddSingleton<IVectorRepository, QdrantVectorRepository>();

        // Ingestion
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));
        services.AddScoped<IRepositoryCloner, RepositoryCloner>();
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<EmbeddingPipeline>();
        services.AddScoped<IIngestionPipeline, IngestionPipeline>();

        // Document parsers — registered as IDocumentParser, all resolved via IEnumerable<IDocumentParser>
        services.AddScoped<IDocumentParser, PdfParser>();
        services.AddScoped<IDocumentParser, DocxParser>();
        services.AddScoped<IDocumentParser, MarkdownParser>();
        services.AddScoped<IDocumentParser, PlainTextParser>();

        return services;
    }
}
