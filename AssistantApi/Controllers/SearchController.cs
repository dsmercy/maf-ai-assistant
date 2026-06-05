using AssistantApi.Application.DTOs;
using AssistantApi.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

/// <summary>
/// Provides direct semantic search against the Qdrant vector collections.
/// Useful for testing whether repositories have been indexed correctly
/// and for verifying that the RAG pipeline retrieves relevant results.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly IValidator<SearchRequest> _validator;
    private readonly IConfiguration _config;

    public SearchController(
        IOllamaClient ollama,
        IVectorRepository vectors,
        IValidator<SearchRequest> validator,
        IConfiguration config)
    {
        _ollama = ollama;
        _vectors = vectors;
        _validator = validator;
        _config = config;
    }

    /// <summary>
    /// Embeds the query text and performs a cosine similarity search against the specified collection.
    /// Supports optional filtering by repository name and programming language.
    /// </summary>
    /// <param name="q">The search query text.</param>
    /// <param name="collection">Qdrant collection to search. One of: code-embeddings, doc-embeddings, instruction-embeddings.</param>
    /// <param name="topK">Maximum number of results to return (1–20).</param>
    /// <param name="repository">Optional: restrict results to a specific repository name.</param>
    /// <param name="language">Optional: restrict results to a specific programming language.</param>
    [HttpGet]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery] string q = "",
        [FromQuery] string collection = "code-embeddings",
        [FromQuery] int topK = 5,
        [FromQuery] string? repository = null,
        [FromQuery] string? language = null,
        CancellationToken ct = default)
    {
        var request = new SearchRequest
        {
            Query = q ?? string.Empty,
            Collection = collection,
            TopK = topK,
            RepositoryFilter = repository,
            LanguageFilter = language
        };

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var embeddingModel = _config["Assistant:EmbeddingModel"] ?? "nomic-embed-text";
        var vector = await _ollama.EmbedAsync(embeddingModel, q ?? string.Empty, ct);

        var filters = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(repository)) filters["repository"] = repository;
        if (!string.IsNullOrWhiteSpace(language)) filters["language"] = language;

        var results = await _vectors.SearchAsync(collection, vector, topK,
            filters.Count > 0 ? filters : null, ct);

        return Ok(new SearchResponse
        {
            Query = q ?? string.Empty,
            Collection = collection,
            Results = results.Select(r => new SearchResult
            {
                Content = r.Content,
                Score = r.Score,
                Metadata = r.Metadata
            }).ToList()
        });
    }
}
