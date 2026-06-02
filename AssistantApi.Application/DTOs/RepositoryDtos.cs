namespace AssistantApi.Application.DTOs;

public class RegisterRepositoryRequest
{
    public string Url { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string? Pat { get; set; }
}

public class RepositoryResponse
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int ChunkCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastIndexedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class IngestionJobResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string Collection { get; set; } = "code-embeddings";
    public int TopK { get; set; } = 5;
    public string? RepositoryFilter { get; set; }
    public string? LanguageFilter { get; set; }
}

public class SearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public List<SearchResult> Results { get; set; } = [];
}

public class SearchResult
{
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
