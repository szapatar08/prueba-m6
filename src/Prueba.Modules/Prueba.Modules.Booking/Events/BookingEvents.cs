using Prueba.Domain.Events;

namespace Prueba.Modules.Booking.Events;

public record BookingConfirmed(Guid BookingId) : IDomainEvent;
public record BookingCancelled(Guid BookingId) : IDomainEvent;
