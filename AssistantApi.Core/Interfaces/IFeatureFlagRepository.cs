using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

/// <summary>Persistence contract for feature flags.</summary>
public interface IFeatureFlagRepository
{
    Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default);
}
