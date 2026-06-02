using AssistantApi.Application.DTOs;
using FluentValidation;

namespace AssistantApi.Application.Validators;

public class RegisterRepositoryRequestValidator : AbstractValidator<RegisterRepositoryRequest>
{
    public RegisterRepositoryRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                         && (uri.Scheme == "https" || uri.Scheme == "http"))
            .WithMessage("Url must be a valid HTTP/HTTPS URL.");

        RuleFor(x => x.Branch)
            .NotEmpty()
            .MaximumLength(256);
    }
}

public class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    private static readonly HashSet<string> ValidCollections =
        ["code-embeddings", "doc-embeddings", "instruction-embeddings"];

    public SearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(x => x.Collection)
            .Must(c => ValidCollections.Contains(c))
            .WithMessage("Collection must be one of: code-embeddings, doc-embeddings, instruction-embeddings.");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, 20);
    }
}
