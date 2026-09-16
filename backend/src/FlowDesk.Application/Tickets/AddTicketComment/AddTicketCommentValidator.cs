using FluentValidation;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.AddTicketComment;

public sealed class AddTicketCommentValidator : AbstractValidator<AddTicketCommentCommand>
{
    public AddTicketCommentValidator()
    {
        RuleFor(command => command.Body)
            .NotEmpty().WithMessage("Yorum boş olamaz.")
            .MaximumLength(TicketComment.MaximumBodyLength)
                .WithMessage($"Yorum en fazla {TicketComment.MaximumBodyLength} karakter olabilir.");
    }
}
