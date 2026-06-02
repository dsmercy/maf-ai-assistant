namespace AssistantApi.Core.Interfaces;

public interface IChunkingService
{
    IReadOnlyList<string> Chunk(string text, int chunkSize, int overlap);
}
