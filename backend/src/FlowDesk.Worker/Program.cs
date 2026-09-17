using FlowDesk.Application.Team;
using FlowDesk.Application.Tickets;
using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Notifications;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFlowDeskInfrastructure(builder.Configuration);

/*
  Consumers run here and only here. The API publishes but does not consume: if
  both hosts read the same queues, every message would be handled twice and the
  second handling would be invisible in the API's own logs.
*/
builder.Services.AddFlowDeskMessageConsumers();

/*
  Drains the outbox into the broker. Use cases queue messages by writing a row
  in their own transaction; nothing reaches RabbitMQ until this runs
  (ADR-0033).
*/
builder.Services.AddFlowDeskOutboxProcessor(builder.Configuration);

/*
  The consumers.

  A queue per message type, and the queue name is also the consumer's name in
  the processed-message table (ADR-0033) — so these names are a contract, not a
  label. Renaming one makes every message it has already handled look unhandled.
*/
builder.Services.AddMessageConsumer<TicketAssigned, TicketAssignedConsumer>(
    queueName: TicketAssignedConsumer.QueueName,
    routingPattern: TicketAssigned.Key);

builder.Services.AddMessageConsumer<TicketCommented, TicketCommentedConsumer>(
    queueName: TicketCommentedConsumer.QueueName,
    routingPattern: TicketCommented.Key);

builder.Services.AddMessageConsumer<MemberInvited, MemberInvitedConsumer>(
    queueName: MemberInvitedConsumer.QueueName,
    routingPattern: MemberInvited.Key);

var host = builder.Build();
await host.RunAsync();
