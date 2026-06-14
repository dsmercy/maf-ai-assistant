using AssistantApi.Application.Configuration;
using AssistantApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AssistantApi.Extensions;

public static class StartupExtensions
{
    public static async Task InitialiseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.MigratePostgresAsync();
        await sp.EnsureQdrantCollectionsAsync();
        await sp.SeedDataAsync();
    }

    private static async Task MigratePostgresAsync(this IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AssistantDbContext>();
        await db.Database.MigrateAsync();
    }

    private static async Task EnsureQdrantCollectionsAsync(this IServiceProvider sp)
    {
        var qdrant  = sp.GetRequiredService<QdrantClient>();
        var options = sp.GetRequiredService<IOptions<AssistantOptions>>().Value;

        string[] collections = ["code-embeddings", "doc-embeddings", "instruction-embeddings"];
        var existing = await qdrant.ListCollectionsAsync();

        foreach (var name in collections.Except(existing))
        {
            await qdrant.CreateCollectionAsync(name, new VectorParams
            {
                Size     = options.VectorSize,
                Distance = Distance.Cosine,
            });
        }
    }

    private static async Task SeedDataAsync(this IServiceProvider sp)
    {
        var flagRepo     = sp.GetRequiredService<AssistantApi.Core.Interfaces.IFeatureFlagRepository>();
        var templateRepo = sp.GetRequiredService<AssistantApi.Core.Interfaces.IPromptTemplateRepository>();

        await flagRepo.SeedFeatureFlagsAsync();
        await templateRepo.SeedPromptTemplatesAsync();
    }
}
