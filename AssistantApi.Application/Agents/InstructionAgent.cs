using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Retrieves relevant coding standards from instruction-embeddings using semantic search.
///
/// INSTRUCTION FILE TAXONOMY
/// =========================
/// Each instruction file has a YAML front matter with a `language` tag and a `category` tag.
/// The `language` tag is used as a Qdrant metadata filter so only relevant rules are retrieved.
///
/// General cross-stack files (instructions/general/):
///   language=general              — core principles, change risk, knowledge integrity
///   language=general-security     — OWASP, injection, secrets, SSRF, crypto, CORS, containers
///   language=general-testing      — Arrange/Act/Assert, naming, frameworks, coverage expectations
///   language=general-api          — contract safety, HTTP status codes, versioning, ProblemDetails
///   language=general-observability— structured logging, OpenTelemetry, correlation IDs
///   language=general-dependencies — stdlib-first, pin versions, audit, licensing
///   language=general-database     — migrations, EF Core SQL safety, parameterized queries
///
/// C# files (instructions/csharp/) — language=csharp:
///   category: architecture        — Clean Architecture, dependency direction, layer responsibilities
///   category: domain-modelling    — entities, value objects, domain events, domain services, DI
///   category: cqrs-mediatr        — commands, queries, handlers, pipeline behaviours
///   category: ef-core             — AsNoTracking, N+1, split queries, optimistic concurrency
///   category: async-patterns      — CancellationToken, IAsyncEnumerable, SemaphoreSlim, anti-patterns
///   category: error-handling      — typed exceptions, result pattern, Polly retry, ProblemDetails
///   category: code-quality        — records, nullable, guard clauses, anti-patterns, Options pattern
///   category: aspnetcore-api      — thin controllers, FluentValidation, authorisation, OpenAPI
///   category: csharp-testing      — xUnit, Moq, FluentAssertions, WebApplicationFactory
///   category: csharp-observability— ILogger/Serilog structured logging, OTel, Serilog config
///
/// Node.js files (instructions/javascript/) — language=javascript:
///   category: architecture        — layered structure, ES modules, DI
///   category: express-standards   — controllers, middleware, route design, Zod validation
///   category: async-concurrency   — Promise.all, anti-patterns, event loop, built-ins
///   category: error-handling      — typed errors, global middleware, consistent error shape
///   category: background-processing — BullMQ, graceful shutdown, SIGTERM/SIGINT
///   category: nodejs-configuration— env vars, Zod config validation, Pino structured logging
///   category: nodejs-testing      — Vitest, Supertest, Arrange/Act/Assert, MSW
///   category: nodejs-database     — parameterized queries, ORM/query builders
///
/// React/TS files (instructions/typescript/) — language=typescript:
///   category: architecture        — feature-first structure, TypeScript strict, naming, imports
///   category: react-api-layer     — centralised client, Zod validation, env vars
///   category: react-query         — query key factories, staleTime, mutations, invalidation
///   category: state-management    — Zustand, useState vs useMemo vs useEffect rules
///   category: react-components    — named exports, custom hooks, forms (RHF+Zod), error boundaries
///   category: accessibility       — semantic HTML, keyboard nav, ARIA, modals, Tailwind
///   category: react-performance   — code splitting, bundle monitoring, no premature memoisation
///   category: react-error-handling— error boundaries, toUserMessage, async state (loading/error/empty)
///   category: react-testing       — Vitest+RTL+MSW, query priority, behaviour-first
///   category: react-observability — centralised logging, security (CSP, no localStorage secrets)
///
/// Python files (instructions/python/) — language=python:
///   category: python-architecture — project structure, type annotations, Pydantic v2, anti-patterns
///   category: fastapi-standards   — Depends(), StreamingResponse, versioning, BaseSettings
///   category: python-async        — httpx.AsyncClient, Semaphore, aiofiles, streaming
///   category: python-error-handling— typed exceptions, result pattern, tenacity, never swallow
///   category: python-logging      — structlog key=value, never f-strings, correlation IDs
///   category: agent-safety        — is_path_safe(), command allow-list, loop depth, untrusted data
///   category: python-testing      — pytest, AsyncMock, respx, tmp_path, naming
///
/// RETRIEVAL STRATEGY
/// ==================
/// For every request two Qdrant searches run in PARALLEL:
///   1. General tags — cross-stack rules scoped to the intent-relevant categories
///   2. Language tag — language-specific rules for the detected language
/// Total latency = max(search1, search2), not sum.
/// TopK is split across general tags so total prompt size stays bounded.
/// Results are merged (general first = highest priority) and deduplicated by content.
/// </summary>
public class InstructionAgent : IAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly AssistantOptions _options;
    private readonly ILogger<InstructionAgent> _logger;

    /// <summary>
    /// Maps keywords in the user message or code chunk language field
    /// to the normalised language tag used in instruction file front matter.
    /// </summary>
    private static readonly Dictionary<string, string> LanguageTagMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // C# / .NET
        ["csharp"]      = "csharp",
        ["c#"]          = "csharp",
        [".net"]        = "csharp",
        ["dotnet"]      = "csharp",
        ["asp.net"]     = "csharp",
        ["aspnet"]      = "csharp",
        ["ef core"]     = "csharp",
        ["efcore"]      = "csharp",
        ["mediatr"]     = "csharp",
        ["blazor"]      = "csharp",
        ["polly"]       = "csharp",
        ["xunit"]       = "csharp",
        // TypeScript / React / Angular
        ["typescript"]  = "typescript",
        ["ts"]          = "typescript",
        ["tsx"]         = "typescript",
        ["react"]       = "typescript",
        ["angular"]     = "typescript",
        ["next.js"]     = "typescript",
        ["nextjs"]      = "typescript",
        ["zustand"]     = "typescript",
        ["vite"]        = "typescript",
        // JavaScript / Node.js
        ["javascript"]  = "javascript",
        ["js"]          = "javascript",
        ["jsx"]         = "javascript",
        ["node.js"]     = "javascript",
        ["nodejs"]      = "javascript",
        ["express"]     = "javascript",
        ["vue"]         = "javascript",
        ["bullmq"]      = "javascript",
        // Python
        ["python"]      = "python",
        ["py"]          = "python",
        ["fastapi"]     = "python",
        ["pydantic"]    = "python",
        ["langchain"]   = "python",
        ["pytest"]      = "python",
        // Other languages (no language-specific files yet, fall back to general)
        ["go"]          = "go",
        ["golang"]      = "go",
        ["java"]        = "java",
        ["rust"]        = "rust",
        ["ruby"]        = "ruby",
    };

    /// <summary>
    /// For each AgentIntent, the ordered list of general language tags to search.
    /// Only tags relevant to the intent are included — this prevents injecting
    /// unrelated rules (e.g. testing standards into a code review request).
    /// </summary>
    private static readonly Dictionary<AgentIntent, IReadOnlyList<string>> IntentGeneralTagMap = new()
    {
        // Code generation needs architecture rules, API safety, DB safety, and dependency governance.
        [AgentIntent.CodeGeneration] =
        [
            "general",
            "general-api",
            "general-database",
            "general-dependencies",
            "general-observability",
        ],
        // Code review needs change risk classification, security checklist, API contracts, DB safety.
        [AgentIntent.CodeReview] =
        [
            "general",
            "general-security",
            "general-api",
            "general-database",
        ],
        // Unit tests need testing standards and general principles only.
        [AgentIntent.UnitTest] =
        [
            "general",
            "general-testing",
        ],
        // Documentation needs general principles and API standards.
        [AgentIntent.Documentation] =
        [
            "general",
            "general-api",
        ],
        // Explanation and question intents need general principles for context only.
        [AgentIntent.CodeExplanation] =
        [
            "general",
        ],
        [AgentIntent.RepositoryQuestion] =
        [
            "general",
        ],
        [AgentIntent.GeneralQuestion] =
        [
            "general",
        ],
    };

    public string Name => "InstructionAgent";

    public InstructionAgent(
        IOllamaClient ollama,
        IVectorRepository vectors,
        IOptions<AssistantOptions> options,
        ILogger<InstructionAgent> logger)
    {
        _ollama   = ollama;
        _vectors  = vectors;
        _options  = options.Value;
        _logger   = logger;
    }

    /// <summary>
    /// Runs two parallel Qdrant searches then merges results:
    ///   1. General cross-stack rules — filtered to the intent-relevant general tags.
    ///   2. Language-specific rules   — filtered by the detected language tag.
    /// Merges with general rules first (highest priority), deduplicates by content.
    /// Falls back to unfiltered language search if the filtered search returns nothing.
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            var query       = BuildInstructionQuery(context);
            var vector      = await _ollama.EmbedAsync(_options.EmbeddingModel, query, context.CancellationToken);
            var languageTag = DetectLanguageTag(context);

            // General rules are baked into the Modelfile system prompt — no need to retrieve them.
            // var generalTags    = GetGeneralTagsForIntent(context.Intent);
            // var generalTask    = SearchGeneralRulesAsync(vector, generalTags, context.CancellationToken);
            // await Task.WhenAll(generalTask, languageTask);
            // var generalResults = generalTask.Result;

            var languageResults = await SearchLanguageRulesAsync(vector, languageTag, context.CancellationToken);

            // If language-filtered search returned nothing, retry unfiltered.
            if (languageResults.Count == 0 && languageTag is not null)
            {
                _logger.LogDebug(
                    "No language-filtered instructions for language={Language}, retrying unfiltered",
                    languageTag);
                languageResults = await _vectors.SearchAsync(
                    "instruction-embeddings", vector, _options.InstructionTopK,
                    null, context.CancellationToken);
            }

            context.InstructionRules = languageResults
                .Where(r => !string.IsNullOrWhiteSpace(r.Content))
                .Select(r => r.Content)
                .ToList();

            _logger.LogInformation(
                "InstructionAgent retrieved {Total} language-specific rules [{Language}] " +
                "for conversation {ConversationId}",
                context.InstructionRules.Count, languageTag ?? "unfiltered",
                context.ConversationId);

            return new AgentResult { Success = true, Response = string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InstructionAgent failed — continuing without instructions");
            return new AgentResult { Success = false, Response = string.Empty, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Searches each general tag concurrently, distributing the TopK budget across tags
    /// so total general rules stay bounded regardless of how many tags are searched.
    /// Deduplicates across tag results before returning.
    /// </summary>
    private async Task<IReadOnlyList<VectorSearchResult>> SearchGeneralRulesAsync(
        float[] vector,
        IReadOnlyList<string> tags,
        CancellationToken ct)
    {
        if (tags.Count == 0) return [];

        // Divide TopK budget across tags — e.g. 5 TopK / 4 tags = 1-2 results per tag.
        var perTagK = Math.Max(1, _options.InstructionTopK / tags.Count);

        var tasks = tags.Select(tag => _vectors.SearchAsync(
            "instruction-embeddings", vector, perTagK,
            new Dictionary<string, string> { ["language"] = tag },
            ct));

        var allResults = await Task.WhenAll(tasks);

        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<VectorSearchResult>();

        foreach (var result in allResults.SelectMany(r => r))
        {
            if (!string.IsNullOrWhiteSpace(result.Content) && seen.Add(result.Content))
                merged.Add(result);
        }

        return merged;
    }

    /// <summary>
    /// Searches for language-specific rules using the detected language tag filter.
    /// Falls back to an unfiltered search if no language was detected.
    /// </summary>
    private Task<IReadOnlyList<VectorSearchResult>> SearchLanguageRulesAsync(
        float[] vector,
        string? languageTag,
        CancellationToken ct)
    {
        if (languageTag is null)
        {
            return _vectors.SearchAsync(
                "instruction-embeddings", vector, _options.InstructionTopK,
                null, ct);
        }

        _logger.LogDebug("InstructionAgent language filter: {Language}", languageTag);
        return _vectors.SearchAsync(
            "instruction-embeddings", vector, _options.InstructionTopK,
            new Dictionary<string, string> { ["language"] = languageTag },
            ct);
    }

    /// <summary>
    /// Returns the general tag list for the given intent.
    /// Falls back to ["general"] for unrecognised intents.
    /// </summary>
    private static IReadOnlyList<string> GetGeneralTagsForIntent(AgentIntent intent) =>
        IntentGeneralTagMap.TryGetValue(intent, out var tags) ? tags : ["general"];

    /// <summary>
    /// Detects the programming language from the user message first (keyword scan),
    /// then falls back to the dominant language in the retrieved code chunks.
    /// Returns the normalised language tag or null if no language is identifiable.
    /// </summary>
    private static string? DetectLanguageTag(AgentContext context)
    {
        var lower = context.UserMessage.ToLowerInvariant();

        foreach (var (keyword, tag) in LanguageTagMap)
        {
            if (lower.Contains(keyword))
                return tag;
        }

        var chunkLanguage = context.RetrievedChunks
            .Select(c => c.Language)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        if (chunkLanguage is not null &&
            LanguageTagMap.TryGetValue(chunkLanguage, out var mappedTag))
            return mappedTag;

        return null;
    }

    /// <summary>
    /// Builds the semantic search query from intent-specific terms plus the general tag
    /// hint keywords. The tag hints move the embedding closer to instruction chunks
    /// whose front matter categories match the current intent.
    /// </summary>
    private static string BuildInstructionQuery(AgentContext context)
    {
        var intentTerms = context.Intent switch
        {
            AgentIntent.CodeGeneration     => "coding standards architecture dependency safety api contract database",
            AgentIntent.CodeReview         => "code review change risk security checklist quality forbidden patterns",
            AgentIntent.UnitTest           => "unit testing standards naming conventions mocking arrange act assert",
            AgentIntent.Documentation      => "documentation standards public api comments conventions",
            AgentIntent.CodeExplanation    => "code explanation architecture patterns conventions",
            AgentIntent.RepositoryQuestion => "codebase architecture conventions structure",
            _                              => "core principles engineering standards correctness safety"
        };

        var tagHint = string.Join(" ", GetGeneralTagsForIntent(context.Intent));
        return $"{intentTerms} {tagHint}";
    }
}
