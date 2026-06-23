using Prueba.Application.Interfaces;
using Prueba.Domain.Events;

namespace Prueba.Infrastructure.Services;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        // TODO: Implement via MediatR or direct handler resolution in PR 7 (Notifications)
        foreach (var domainEvent in domainEvents)
        {
            // Placeholder — will be implemented with MediatR in Notifications module
        }

        return Task.CompletedTask;
    }
}
