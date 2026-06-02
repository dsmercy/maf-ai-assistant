using AssistantApi.Application.DTOs;
using FluentValidation;

namespace AssistantApi.Application.Validators;

public class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(32_000).WithMessage("Message must not exceed 32,000 characters.");

        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("ConversationId is required.")
            .MaximumLength(128);
    }
}
