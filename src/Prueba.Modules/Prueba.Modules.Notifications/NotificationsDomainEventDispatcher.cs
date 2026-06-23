using Hangfire;
using Prueba.Application.Interfaces;
using Prueba.Domain.Events;
using Prueba.Modules.Booking.Events;
using Prueba.Modules.KYC.Events;
using Prueba.Modules.Notifications.Handlers;

namespace Prueba.Modules.Notifications;

public class NotificationsDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public NotificationsDomainEventDispatcher(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case BookingConfirmed bookingConfirmed:
                    _backgroundJobClient.Enqueue<BookingConfirmedEventHandler>(
                        handler => handler.HandleAsync(
                            bookingConfirmed.BookingId,
                            Guid.Empty,
                            CancellationToken.None));
                    break;

                case BookingCancelled bookingCancelled:
                    _backgroundJobClient.Enqueue<BookingCancelledEventHandler>(
                        handler => handler.HandleAsync(
                            bookingCancelled.BookingId,
                            Guid.Empty,
                            CancellationToken.None));
                    break;

                case KycCompleted kycCompleted:
                    _backgroundJobClient.Enqueue<KycCompletedEventHandler>(
                        handler => handler.HandleAsync(
                            kycCompleted.UserId,
                            kycCompleted.Status.ToString(),
                            CancellationToken.None));
                    break;
            }
        }

        return Task.CompletedTask;
    }
}
