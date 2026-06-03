using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

public interface IPromptTemplateRepository
{
    Task<PromptTemplate?> GetByTaskTypeAsync(string taskType, CancellationToken ct = default);
    Task<IReadOnlyList<PromptTemplate>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(PromptTemplate template, CancellationToken ct = default);
}
