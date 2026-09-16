using FluentValidation;
using FlowDesk.Domain.Customers;

namespace FlowDesk.Application.Customers.UpdateCustomer;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Müşteri adı zorunludur.")
            .MaximumLength(Customer.MaximumNameLength)
                .WithMessage($"Müşteri adı en fazla {Customer.MaximumNameLength} karakter olabilir.");

        When(command => !string.IsNullOrWhiteSpace(command.Email), () =>
        {
            RuleFor(command => command.Email!)
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
                .MaximumLength(Customer.MaximumEmailLength)
                    .WithMessage($"E-posta adresi en fazla {Customer.MaximumEmailLength} karakter olabilir.");
        });

        RuleFor(command => command.Phone)
            .MaximumLength(Customer.MaximumPhoneLength)
                .WithMessage($"Telefon en fazla {Customer.MaximumPhoneLength} karakter olabilir.");

        RuleFor(command => command.Company)
            .MaximumLength(Customer.MaximumCompanyLength)
                .WithMessage($"Şirket adı en fazla {Customer.MaximumCompanyLength} karakter olabilir.");

        RuleFor(command => command.Notes)
            .MaximumLength(Customer.MaximumNotesLength)
                .WithMessage($"Notlar en fazla {Customer.MaximumNotesLength} karakter olabilir.");

        RuleFor(command => command.Status)
            .IsInEnum().WithMessage("Geçerli bir durum seçin.");
    }
}
