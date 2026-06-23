using Prueba.Domain.Events;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Events;

public record KycCompleted(Guid ValidationId, Guid UserId, KycStatus Status) : IDomainEvent;
