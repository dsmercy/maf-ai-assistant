using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;

namespace AssistantApi.Extensions;

public static class SeedDataExtensions
{
    public static async Task SeedFeatureFlagsAsync(this IFeatureFlagRepository repo)
    {
        var existing = (await repo.GetAllAsync()).Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaults = new[]
        {
            (name: "code-embeddings",        enabled: false, desc: "Source-code RAG — search code-embeddings collection. Disable when no repositories are indexed."),
            (name: "doc-embeddings",         enabled: true,  desc: "Document RAG — search doc-embeddings collection for uploaded PDFs, DOCX, and Markdown."),
            (name: "instruction-embeddings", enabled: true,  desc: "Instruction RAG — search instruction-embeddings collection for uploaded coding standards and rules."),
        };

        foreach (var (name, enabled, desc) in defaults.Where(f => !existing.Contains(f.name)))
        {
            await repo.UpsertAsync(new FeatureFlag
            {
                Name        = name,
                IsEnabled   = enabled,
                Description = desc
            });
        }
    }

    public static async Task SeedPromptTemplatesAsync(this IPromptTemplateRepository repo)
    {
        var existing = (await repo.GetAllAsync())
            .Select(t => t.TaskType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaults = new[]
        {
            new PromptTemplate
            {
                Name               = "Code Generation",
                TaskType           = "CodeGeneration",
                SystemPrompt       = "You are an expert software engineer. Generate clean, production-ready code.\nFollow these coding standards strictly:\n{instructions}\n\nUse the following context from documentation as reference:\n{context_chunks}",
                UserPromptTemplate = "Generate the following: {user_message}\n\nLanguage/Framework: {language}\nProvide complete, working code with no placeholders.",
                IsActive           = true
            },
            new PromptTemplate
            {
                Name               = "Code Review",
                TaskType           = "CodeReview",
                SystemPrompt       = "You are a senior code reviewer. Review code for correctness, maintainability, and standards compliance.\nCoding standards to enforce:\n{instructions}\n\nRelevant context:\n{context_chunks}",
                UserPromptTemplate = "Review the following: {user_message}\n\nIdentify: bugs, standards violations, improvements. Be specific and actionable.",
                IsActive           = true
            },
            new PromptTemplate
            {
                Name               = "Unit Test Generation",
                TaskType           = "UnitTest",
                SystemPrompt       = "You are an expert in software testing. Generate comprehensive unit tests.\nTesting standards:\n{instructions}\n\nCode under test:\n{context_chunks}",
                UserPromptTemplate = "Generate unit tests for: {user_message}\n\nLanguage: {language}\nInclude: arrange/act/assert, edge cases, meaningful test names.",
                IsActive           = true
            },
            new PromptTemplate
            {
                Name               = "Documentation",
                TaskType           = "Documentation",
                SystemPrompt       = "You are a technical writer and software engineer. Generate clear documentation.\nDocumentation standards:\n{instructions}\n\nCode context:\n{context_chunks}",
                UserPromptTemplate = "Generate documentation for: {user_message}\n\nInclude: purpose, parameters, return values, examples where appropriate.",
                IsActive           = true
            },
            new PromptTemplate
            {
                Name               = "Code Explanation",
                TaskType           = "CodeExplanation",
                SystemPrompt       = "You are an expert software engineer. Explain code clearly and concisely.\nCoding standards for reference:\n{instructions}\n\nRelevant context:\n{context_chunks}",
                UserPromptTemplate = "{user_message}\n\nExplain clearly. Reference specific lines or patterns where relevant.",
                IsActive           = true
            },
            new PromptTemplate
            {
                Name               = "Repository Question",
                TaskType           = "RepositoryQuestion",
                SystemPrompt       = "You are an expert software engineer with full knowledge of this codebase.\nCoding standards:\n{instructions}\n\nRelevant context from the repository:\n{context_chunks}",
                UserPromptTemplate = "{user_message}\n\nBase your answer on the provided context. Reference specific files and patterns.",
                IsActive           = true
            },
            new PromptTemplate
            {
                Name               = "General Question",
                TaskType           = "GeneralQuestion",
                SystemPrompt       = "You are an expert software engineering assistant.\n{instructions}",
                UserPromptTemplate = "{user_message}",
                IsActive           = true
            },
        };

        foreach (var template in defaults.Where(t => !existing.Contains(t.TaskType)))
            await repo.UpsertAsync(template);
    }
}
