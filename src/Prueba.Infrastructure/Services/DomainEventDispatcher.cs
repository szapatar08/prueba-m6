using Prueba.Application.Interfaces;
using Prueba.Domain.Events;

namespace Prueba.Infrastructure.Services;

/// <summary>
/// Default no-op dispatcher. Modules can override registration with their own implementation.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        // Default no-op. Notifications module registers its own dispatcher.
        return Task.CompletedTask;
    }
}
