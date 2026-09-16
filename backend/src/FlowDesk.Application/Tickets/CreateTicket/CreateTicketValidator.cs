using FluentValidation;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.CreateTicket;

public sealed class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(command => command.CustomerId)
            .NotEmpty().WithMessage("Müşteri seçimi zorunludur.");

        RuleFor(command => command.Subject)
            .NotEmpty().WithMessage("Konu zorunludur.")
            .MaximumLength(Ticket.MaximumSubjectLength)
                .WithMessage($"Konu en fazla {Ticket.MaximumSubjectLength} karakter olabilir.");

        RuleFor(command => command.Description)
            .MaximumLength(Ticket.MaximumDescriptionLength)
                .WithMessage($"Açıklama en fazla {Ticket.MaximumDescriptionLength} karakter olabilir.");

        RuleFor(command => command.Priority)
            .IsInEnum().WithMessage("Geçerli bir öncelik seçin.");
    }
}
