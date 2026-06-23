# Prueba — Multi-Tenant Property Management Platform

A modular monolith built with .NET 10, ASP.NET Core, EF Core, and PostgreSQL.

## Architecture

- **Modular Monolith** with Clean Architecture layers
- **Vertical Slice** organization within modules
- **Schema-per-tenant** multi-tenant isolation
- **Domain-Driven Design** primitives (Entities, Value Objects, Domain Events)

### Project Structure

```
src/
├── Prueba.Api/                    # ASP.NET Core host, controllers, middleware
├── Prueba.Application/            # Shared abstractions, interfaces, common
├── Prueba.Domain/                 # Domain primitives (entities, value objects, events)
├── Prueba.Infrastructure/         # EF Core, Hangfire, MinIO, MailKit
└── Prueba.Modules/
    ├── Prueba.Modules.Identity/   # Authentication, authorization, JWT
    ├── Prueba.Modules.Properties/ # Property CRUD, availability, catalog
    ├── Prueba.Modules.Booking/    # Reservations, double-booking prevention
    ├── Prueba.Modules.Wishlist/   # Guest favorites
    ├── Prueba.Modules.KYC/        # Identity verification, document processing
    ├── Prueba.Modules.Notifications/ # Email, in-app notifications
    ├── Prueba.Modules.Dashboard/  # Owner metrics, occupancy, revenue
    └── Prueba.Modules.Reports/    # Excel report generation
tests/
├── Prueba.UnitTests/              # Unit tests (xUnit, Moq, FluentAssertions)
└── Prueba.IntegrationTests/       # Integration tests (WebApplicationFactory, Testcontainers)
docker/
├── docker-compose.yml             # PostgreSQL, Redis, MinIO
└── Dockerfile                     # Multi-stage build
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started) + Docker Compose
- Node.js 20+ (frontend)

## Quick Start

### 1. Start infrastructure

```bash
docker-compose -f docker/docker-compose.yml up -d
```

This starts:
- PostgreSQL 16 (port 5432)
- Redis 7 (port 6379)
- MinIO (ports 9000/9001)

### 2. Restore and build

```bash
dotnet restore
dotnet build
```

### 3. Run the API

```bash
dotnet run --project src/Prueba.Api
```

The API will be available at:
- **Swagger UI**: https://localhost:5001/swagger
- **Health Check**: https://localhost:5001/health
- **Hangfire Dashboard**: https://localhost:5001/hangfire

### 4. Run tests

```bash
# Unit tests
dotnet test tests/Prueba.UnitTests

# Integration tests
dotnet test tests/Prueba.IntegrationTests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Configuration

### Database Connection

Default connection string in `appsettings.json`:

```
Host=localhost;Database=prueba;Username=postgres;Password=postgres
```

### JWT Settings

```json
{
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "PruebaApi",
    "Audience": "PruebaClient",
    "ExpiresInMinutes": 60
  }
}
```

**Important**: Change the JWT key in production!

## Module Development

Each module follows Vertical Slice architecture:

```
Prueba.Modules.{Name}/
├── Features/          # Use cases (Command/Query + Handler + Validator)
│   ├── {FeatureName}/
│   │   ├── {Feature}Command.cs
│   │   ├── {Feature}Validator.cs
│   │   └── {Feature}Handler.cs
├── Entities/          # Module-specific domain entities
├── Data/              # EF Core configurations
└── Events/            # Domain events
```

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Tenant isolation | Schema-per-tenant | Stronger isolation, eliminates cross-tenant leak risk |
| Module structure | Vertical Slice | Better cohesion per feature |
| Booking concurrency | SELECT FOR UPDATE | Safer for money-adjacent operations |
| Background jobs | Hangfire | Retry, dashboard, persistence out-of-the-box |
| Object storage | MinIO | S3-compatible, encryption, lifecycle policies |
| Email | MailKit/Gmail | Clean TLS/OAuth2 handling |

## License

Proprietary
