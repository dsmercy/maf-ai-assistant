using AssistantApi.Core.Interfaces;
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

        // Ollama
        var ollamaOptions = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>()
                            ?? new OllamaOptions();
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
        {
            client.BaseAddress = new Uri(ollamaOptions.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
        }).AddStandardResilienceHandler();

        // Qdrant
        var qdrantHost = configuration["Qdrant:Host"] ?? "localhost";
        var qdrantPort = int.TryParse(configuration["Qdrant:Port"], out var p) ? p : 6334;
        services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));
        services.AddSingleton<IVectorRepository, QdrantVectorRepository>();

        return services;
    }
}
