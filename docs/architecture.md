# Architecture — Prueba Platform

## Modular Monolith

Prueba is a **Modular Monolith** — a single deployable unit composed of loosely coupled modules. Each module owns its domain logic, data access, and API surface. This architecture provides the simplicity of a monolith while preserving the ability to extract modules into independent services if needed.

### Why Modular Monolith?

| Approach | Pros | Cons |
|----------|------|------|
| Traditional Monolith | Simple deployment | Tight coupling, hard to scale teams |
| Microservices | Independent scaling, deployment | Distributed complexity, network overhead |
| **Modular Monolith** | **Module boundaries, single deployment, future extraction** | **Requires discipline to maintain boundaries** |

### Module Independence

Each module follows Vertical Slice architecture:

```
Prueba.Modules.{Name}/
├── Features/              # Use cases (Command/Query + Handler + Validator)
│   └── {FeatureName}/
│       ├── {Feature}Command.cs
│       ├── {Feature}Validator.cs
│       └── {Feature}Handler.cs
├── Entities/              # Module-specific domain entities
├── Data/                  # EF Core configurations
└── Events/                # Domain events
```

Modules communicate through:
1. **Shared interfaces** (Application layer): `IRepository`, `IUnitOfWork`, `ICurrentTenant`
2. **Domain events**: Asynchronous, decoupled communication via `IDomainEventDispatcher`
3. **Cross-module references**: Only through Application layer interfaces, never direct

---

## Clean Architecture Layers

```
                    ┌──────────────┐
                    │   Domain     │  ← No dependencies
                    │  (Entities,  │
                    │   Events)    │
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
                    │ Application  │  ← Depends on Domain
                    │ (Interfaces, │
                    │  Common)     │
                    └──────┬───────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
       ┌──────▼──────┐ ┌──▼──────────┐ │
       │Infrastructure│ │   Modules   │ │  ← Depend on Application
       │ (EF Core,    │ │ (Identity,  │ │
       │  MinIO,      │ │  Booking,   │ │
       │  MailKit)    │ │  etc.)      │ │
       └──────────────┘ └─────────────┘ │
                           │            │
                    ┌──────▼────────────▼──┐
                    │       API Layer      │  ← Depends on everything
                    │ (Controllers,        │
                    │  Middleware)          │
                    └──────────────────────┘
```

**Dependency Rule**: Dependencies point inward. Domain has zero external dependencies. Application defines interfaces that Infrastructure implements. Modules depend on Application, not on each other directly.

---

## Module Dependency Graph

```
┌──────────────┐
│   Domain     │ ← Foundation: BaseEntity, AggregateRoot, IDomainEvent
└──────┬───────┘
       │
┌──────▼───────┐
│ Application  │ ← Shared: IRepository, IUnitOfWork, ICurrentTenant, Result<T>
└──────┬───────┘
       │
┌──────▼───────┐
│Infrastructure│ ← Implements: AppDbContext, Repository, MinIO, MailKit
└──────┬───────┘
       │
       ├──────────────────────────────────────────────────────────┐
       │                                                          │
┌──────▼───────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐│
│   Identity   │ │  Properties │ │   Booking   │ │   Wishlist  ││
│              │ │             │ │             │ │             ││
│ - User       │ │ - Property  │ │ - Booking   │ │ - Wishlist  ││
│ - Role       │ │ - Image     │ │ - Status    │ │   Item      ││
│ - UserRole   │ │ - Avail.    │ │ - Events    │ │             ││
└──────────────┘ └─────────────┘ └──────┬──────┘ └─────────────┘│
                                        │                       │
                                 ┌──────▼──────┐ ┌─────────────┐│
                                 │     KYC     │ │    Notifications
                                 │             │ │             ││
                                 │ - KycValid. │ │ - Notif.    ││
                                 │ - KycDoc    │ │ - Template  ││
                                 │ - Events    │ │ - Handlers  ││
                                 └─────────────┘ └─────────────┘│
                                                                 │
                                 ┌─────────────┐ ┌─────────────┐│
                                 │  Dashboard  │ │   Reports   ││
                                 │             │ │             ││
                                 │ - Occupancy │ │ - Excel     ││
                                 │ - Revenue   │ │   Gen       ││
                                 │ - Trends    │ │             ││
                                 └─────────────┘ └─────────────┘│
```

**Cross-module dependencies**:
- Booking → KYC (checks approval status via `IKycService`)
- Booking → Properties (validates property exists)
- Notifications → Booking, KYC (listens to domain events)
- Wishlist → Properties (joins for display)
- Dashboard, Reports → Booking, Properties (queries for metrics)

---

## Multi-Tenant Strategy

### Schema-per-Tenant Isolation

Each tenant's data lives in a separate PostgreSQL schema. This provides **database-level isolation** without the overhead of separate databases.

```
PostgreSQL Database
├── public (shared: Tenant registry)
├── tenant_{guid-1}
│   ├── Users
│   ├── Properties
│   ├── Bookings
│   └── ...
├── tenant_{guid-2}
│   ├── Users
│   ├── Properties
│   ├── Bookings
│   └── ...
```

### Implementation

**1. Tenant Resolution Middleware** (runs after authentication):
```csharp
public class TenantResolution(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ICurrentTenant tenant)
    {
        var tenantId = ctx.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantId, out var id))
            tenant.SetTenant(id);  // Sets SchemaName = "tenant_{id}"
        await next(ctx);
    }
}
```

**2. DbContext applies schema per request**:
```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.HasDefaultSchema(_currentTenant.SchemaName);

    // Global query filter on all BaseEntity types
    foreach (var entity in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(BaseEntity).IsAssignableFrom(entity.ClrType))
        {
            // Filter: e.TenantId == currentTenant.TenantId
            builder.Entity(entity.ClrType)
                .HasQueryFilter(/* tenant filter expression */);
        }
    }
}
```

**3. Tenant context from JWT** (never from frontend):
```csharp
// JWT claims include: "tenant_id": "guid"
// TenantResolution middleware extracts and sets on ICurrentTenant
// All queries automatically filtered by tenant
```

### Safety Guarantees

- **Global query filters**: EF Core automatically filters all queries by `TenantId`
- **Manual filters with `IgnoreQueryFilters()`**: Used only in internal lookups (login, KYC check) with explicit tenant scoping
- **Authorization**: `[Authorize]` attributes enforce authentication; tenant ownership validated in handlers
- **Never trust frontend**: Tenant ID always derived from authenticated JWT claims

---

## Double-Booking Prevention

The booking system prevents overlapping reservations using a **SERIALIZABLE transaction** with an **overlap formula**.

### Overlap Formula

```
StartDate < ExistingEndDate  AND  EndDate > ExistingStartDate
```

This catches:
- **Exact same dates**: Aug 10-15 vs Aug 10-15 ✗
- **Partial overlap (start)**: Aug 8-12 vs Aug 10-15 ✗
- **Partial overlap (end)**: Aug 13-18 vs Aug 10-15 ✗
- **Full overlap**: Aug 5-20 vs Aug 10-15 ✗
- **Adjacent (no overlap)**: Aug 15-20 vs Aug 10-15 ✓ (EndDate == StartDate)
- **No overlap**: Aug 1-5 vs Aug 10-15 ✓

### Implementation

```csharp
public async Task<Result<BookingResponse>> Handle(CreateBookingCommand cmd, Guid guestId, CancellationToken ct)
{
    // KYC gate: first booking requires approval
    var hasKyc = await _kycService.HasApprovedKycAsync(guestId, ct);
    if (!hasKyc && !await HasPreviousBookings(guestId, ct))
        return Result.Fail("KYC verification required for first booking.");

    // SERIALIZABLE transaction prevents phantom reads
    await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, ct);

    // Atomic overlap check
    var conflict = await CheckForOverlapAsync(cmd.PropertyId, tenantId, cmd.StartDate, cmd.EndDate, ct);
    if (conflict)
    {
        await _unitOfWork.RollbackTransactionAsync(ct);
        return Result.Fail("Dates unavailable.");
    }

    // Insert booking
    var booking = BookingEntity.Create(cmd.PropertyId, guestId, cmd.StartDate, cmd.EndDate, cmd.TotalPrice, tenantId);
    _repository.Add(booking);
    await _repository.SaveChangesAsync(ct);
    await _unitOfWork.CommitTransactionAsync(ct);

    return Result.Success(new BookingResponse(...));
}
```

### Why SERIALIZABLE?

| Isolation Level | Phantom Reads | Use Case |
|----------------|---------------|----------|
| READ COMMITTED | Possible | General queries |
| REPEATABLE READ | Possible | Consistent reads |
| **SERIALIZABLE** | **Prevented** | **Money-adjacent operations** |

SERIALIZABLE ensures no other transaction can insert a conflicting booking between our overlap check and insert. The slight throughput cost is acceptable for reservation integrity.

### Only Confirmed Bookings Block

Pending bookings do NOT block other bookings. Only `Confirmed` bookings participate in the overlap check. This allows multiple guests to request the same dates — the owner confirms one, and others remain pending.

---

## Background Processing

### Hangfire Pipeline

```
Domain Event (in-process)
    ↓
IDomainEventDispatcher (Notifications module)
    ↓
Hangfire.Enqueue(handler)
    ↓
Background Job (retry: 3 attempts, exponential backoff)
    ↓
Email/Notification/KYC Processing
```

### Jobs

| Job | Trigger | Purpose |
|-----|---------|---------|
| `ProcessKycDocumentJob` | On KYC upload | OCR processing, status update |
| `KycCleanupJob` | Daily (cron) | Delete expired documents (90-day retention) |
| `SendNotificationJob` | On domain event | Email delivery via MailKit |

### Retry Policy

- 3 attempts
- Exponential backoff: 60s, 300s, 900s
- Idempotent: Jobs check current state before processing

---

## Frontend Architecture

### Component Hierarchy

```
App
├── AuthProvider (React Context)
│   └── BrowserRouter
│       ├── Navbar (shared)
│       ├── ProtectedRoute (auth + role guard)
│       │   ├── Login (public)
│       │   ├── Register (public)
│       │   ├── Catalog (public)
│       │   ├── PropertyDetail (public)
│       │   ├── MyBookings (Guest)
│       │   ├── KycUpload (Authenticated)
│       │   ├── Wishlist (Guest)
│       │   ├── Notifications (Authenticated)
│       │   ├── CreateProperty (Owner)
│       │   ├── PropertyBookings (Owner)
│       │   ├── Dashboard (Owner)
│       │   └── Reports (Owner)
```

### API Communication

- **Axios instance** with JWT interceptor (attaches token to every request)
- **401 auto-logout**: Clears token and redirects to login
- **Vite proxy**: Dev server proxies `/api` to `localhost:5000`

### State Management

- **React Context** for authentication (`AuthProvider`, `useAuth`)
- **No global state library**: Server state managed via API calls; UI state local to components
- **Optimistic updates**: Wishlist toggle updates UI immediately, reverts on error
