using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

/// <summary>
/// Shared state object passed through the agent pipeline for a single chat request.
/// Each agent reads from and writes to this context, allowing upstream agents to
/// populate data that downstream agents consume.
/// </summary>
public class AgentContext
{
    /// <summary>The original message from the user.</summary>
    public required string UserMessage { get; init; }

    /// <summary>Identifier that groups messages in the same chat session.</summary>
    public required string ConversationId { get; init; }

    /// <summary>Identity of the requesting user. Defaults to "anonymous" when no JWT is present.</summary>
    public string UserId { get; init; } = "anonymous";

    /// <summary>
    /// The intent classified by OrchestratorAgent. Set before InstructionAgent and RepositoryAgent run.
    /// Determines which prompt template is loaded and whether RepositoryAgent is invoked.
    /// </summary>
    public AgentIntent Intent { get; set; } = AgentIntent.GeneralQuestion;

    /// <summary>
    /// Code chunks retrieved from Qdrant by RepositoryAgent.
    /// Populated only when the intent requires repository context.
    /// CodingAgent injects these into the prompt as code context.
    /// </summary>
    public List<RetrievedChunk> RetrievedChunks { get; set; } = [];

    /// <summary>
    /// Coding standard rules retrieved from the instruction-embeddings Qdrant collection by InstructionAgent.
    /// CodingAgent injects these into the system prompt to enforce team standards.
    /// </summary>
    public List<string> InstructionRules { get; set; } = [];

    /// <summary>
    /// Optional filter to restrict Qdrant search to a specific repository by name.
    /// When null, all indexed repositories are searched.
    /// </summary>
    public string? RepositoryFilter { get; init; }

    /// <summary>
    /// When set, CodingAgent bypasses prompt-template assembly and passes these messages
    /// directly to Ollama. Used when the caller (e.g. Continue) has already composed the
    /// full message array including its own system prompt and tool instructions.
    /// </summary>
    public List<ChatMessage>? MessagesOverride { get; init; }

    /// <summary>Cancellation token propagated from the HTTP request.</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Represents a single code chunk retrieved from Qdrant during semantic search.
/// Carries the content and metadata needed to cite the source in the API response.
/// </summary>
public class RetrievedChunk
{
    /// <summary>The actual source code text of this chunk.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Relative path of the file this chunk came from (e.g. src/Core/Interfaces/IUserRepository.cs).</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Name of the repository this chunk belongs to.</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Programming language of this chunk (e.g. "csharp", "typescript").</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Cosine similarity score between the user query and this chunk (0.0 to 1.0). Higher is more relevant.</summary>
    public double Score { get; set; }

    /// <summary>
    /// Constructs a <see cref="RetrievedChunk"/> from a raw Qdrant search result,
    /// mapping the generic metadata dictionary to typed properties.
    /// </summary>
    public static RetrievedChunk FromSearchResult(VectorSearchResult r) => new()
    {
        Content = r.Content,
        FilePath = r.Metadata.GetValueOrDefault("file_path", string.Empty),
        Repository = r.Metadata.GetValueOrDefault("repository", string.Empty),
        Language = r.Metadata.GetValueOrDefault("language", string.Empty),
        Score = r.Score
    };
}

/// <summary>
/// The result returned by an agent after completing its task.
/// Only CodingAgent sets a meaningful Response value; retrieval agents set Success=true with an empty response.
/// </summary>
public class AgentResult
{
    /// <summary>Whether the agent completed without error.</summary>
    public bool Success { get; set; }

    /// <summary>The generated text response. Empty for retrieval-only agents (InstructionAgent, RepositoryAgent).</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>Error description if Success is false. Null on success.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Total wall-clock time in milliseconds for the full pipeline. Set by OrchestratorAgent.</summary>
    public long LatencyMs { get; set; }

    /// <summary>The intent that was classified for this request. Copied from AgentContext.</summary>
    public AgentIntent Intent { get; set; }
}
