using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFlowDeskInfrastructure(builder.Configuration);

/*
  Consumers run here and only here. The API publishes but does not consume: if
  both hosts read the same queues, every message would be handled twice and the
  second handling would be invisible in the API's own logs.
*/
builder.Services.AddFlowDeskMessageConsumers();

/*
  Consumer registrations go here as the features that need them arrive:

      builder.Services.AddMessageConsumer<TicketAssigned, SendAssignmentEmail>(
          queueName: "flowdesk.ticket-assigned.email",
          routingPattern: "ticket.assigned");

  None are registered yet. The host says so in its log rather than looking busy;
  a consumer that handles nothing would be harder to notice than a line saying
  nothing is subscribed. The first real consumer arrives with notifications
  (Faz 12).
*/

var host = builder.Build();
await host.RunAsync();
