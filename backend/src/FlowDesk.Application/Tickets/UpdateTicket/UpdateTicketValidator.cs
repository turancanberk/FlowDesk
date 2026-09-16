using FluentValidation;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.UpdateTicket;

public sealed class UpdateTicketValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketValidator()
    {
        RuleFor(command => command.Subject)
            .NotEmpty().WithMessage("Konu zorunludur.")
            .MaximumLength(Ticket.MaximumSubjectLength)
                .WithMessage($"Konu en fazla {Ticket.MaximumSubjectLength} karakter olabilir.");

        RuleFor(command => command.Description)
            .MaximumLength(Ticket.MaximumDescriptionLength)
                .WithMessage($"Açıklama en fazla {Ticket.MaximumDescriptionLength} karakter olabilir.");

        RuleFor(command => command.Priority)
            .IsInEnum().WithMessage("Geçerli bir öncelik seçin.");

        RuleFor(command => command.CustomerId)
            .NotEmpty().WithMessage("Müşteri seçimi zorunludur.");
    }
}
