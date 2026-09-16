using FlowDesk.Domain.Customers;

namespace FlowDesk.Application.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    CustomerStatus Status,
    string? Notes);
