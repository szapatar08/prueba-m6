# Prueba — Multi-Tenant Property Management Platform

A production-ready modular monolith built with .NET 10, ASP.NET Core, EF Core, and PostgreSQL. Designed for property owners who manage accommodations and guests who search, reserve, and interact with properties.

---

## Architecture Overview

**Modular Monolith** with Clean Architecture layers and Vertical Slice organization per module. Schema-per-tenant multi-tenant isolation ensures complete data separation between tenants.

```
┌─────────────────────────────────────────────────────────┐
│                     ASP.NET Core API                     │
│  Controllers · Middleware · JWT Auth · Swagger · Hangfire│
├─────────────────────────────────────────────────────────┤
│                    Application Layer                     │
│     Interfaces · Common · DTOs · Validators              │
├─────────────────────────────────────────────────────────┤
│                      Domain Layer                        │
│        Entities · Value Objects · Domain Events          │
├─────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                   │
│      EF Core · PostgreSQL · MinIO · MailKit · Hangfire   │
├─────────────────────────────────────────────────────────┤
│                     Module Layer                         │
│  Identity · Properties · Booking · Wishlist · KYC        │
│  Notifications · Dashboard · Reports                     │
└─────────────────────────────────────────────────────────┘
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Tenant isolation | Schema-per-tenant | Stronger isolation than TenantId column; eliminates cross-tenant leak risk |
| Module structure | Vertical Slice | Better cohesion per feature; each use case is self-contained |
| Booking concurrency | SERIALIZABLE + overlap formula | Prevents phantom reads; atomic check-and-insert |
| Background jobs | Hangfire | Retry, dashboard, persistence out-of-the-box |
| Object storage | MinIO (S3-compatible) | Encryption, lifecycle policies, production parity |
| Email | MailKit via Gmail | Clean TLS handling, free for MVP |
| Frontend | React 19 + TypeScript + Vite | Modern tooling, type safety, fast HMR |

---

## Project Structure

```
src/
├── Prueba.Api/                    # ASP.NET Core host, controllers, middleware
│   ├── Controllers/               # 8 API controllers
│   ├── Middleware/                 # TenantResolution, ExceptionHandling
│   └── Program.cs                 # DI, auth, Hangfire, Swagger
├── Prueba.Application/            # Shared abstractions, interfaces
│   ├── Interfaces/                # IRepository, IUnitOfWork, ICurrentTenant, etc.
│   └── Common/                    # Result<T>, BaseEntity, AggregateRoot
├── Prueba.Domain/                 # Domain primitives (BaseEntity, AggregateRoot)
├── Prueba.Infrastructure/         # EF Core, Hangfire, MinIO, MailKit
│   ├── Data/                      # AppDbContext, migrations
│   └── Services/                  # Repository, KycService, CurrentTenant
└── Prueba.Modules/
    ├── Prueba.Modules.Identity/   # Authentication, JWT, roles
    ├── Prueba.Modules.Properties/ # Property CRUD, availability, catalog
    ├── Prueba.Modules.Booking/    # Reservations, double-booking prevention
    ├── Prueba.Modules.Wishlist/   # Guest favorites
    ├── Prueba.Modules.KYC/        # Identity verification, document processing
    ├── Prueba.Modules.Notifications/ # Email, in-app notifications
    ├── Prueba.Modules.Dashboard/  # Owner metrics (occupancy, revenue, trends)
    └── Prueba.Modules.Reports/    # Excel report generation (.xlsx)

tests/
├── Prueba.UnitTests/              # 313 unit tests (xUnit, Moq, FluentAssertions)
└── Prueba.IntegrationTests/       # 27 integration tests (WebApplicationFactory, SQLite)

frontend/                          # React 19 + TypeScript SPA
├── src/
│   ├── components/                # Navbar, PropertyCard, ProtectedRoute
│   ├── pages/                     # 12 pages (Login, Register, Catalog, etc.)
│   ├── services/                  # Axios API client with JWT interceptor
│   └── context/                   # AuthProvider (React Context)
└── vite.config.ts                 # Tailwind v4, API proxy

docker/
├── docker-compose.yml             # PostgreSQL 16, Redis 7, MinIO
└── Dockerfile                     # Multi-stage .NET build
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started) + Docker Compose
- [Node.js 20+](https://nodejs.org/) (frontend)

---

## Quick Start

### 1. Start infrastructure

```bash
docker-compose -f docker/docker-compose.yml up -d
```

This starts:
- **PostgreSQL 16** on port 5432
- **Redis 7** on port 6379
- **MinIO** on ports 9000 (API) / 9001 (Console)

### 2. Build and run the API

```bash
dotnet restore
dotnet build
dotnet run --project src/Prueba.Api
```

The API will be available at:
- **Swagger UI**: `https://localhost:5001/swagger`
- **Health Check**: `https://localhost:5001/health`
- **Hangfire Dashboard**: `https://localhost:5001/hangfire`

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend available at `http://localhost:5173`.

### 4. Run tests

```bash
# All tests
dotnet test

# Unit tests only (313 tests)
dotnet test tests/Prueba.UnitTests

# Integration tests only (27 tests)
dotnet test tests/Prueba.IntegrationTests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) | From appsettings |
| `GMAIL_ADDRESS` | Gmail address for notifications | — |
| `GMAIL_APP_PASSWORD` | Gmail app password | — |
| `MINIO_ENDPOINT` | MinIO server endpoint | `localhost:9000` |
| `MINIO_ACCESS_KEY` | MinIO access key | `minioadmin` |
| `MINIO_SECRET_KEY` | MinIO secret key | `minioadmin` |

### Database Connection

Default connection string in `appsettings.json`:
```
Host=localhost;Database=prueba;Username=postgres;Password=postgres
```

### JWT Configuration

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

**Important**: Change the JWT key in production using environment variables.

---

## API Documentation

### Authentication

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/auth/register` | Register a new user | Public |
| POST | `/api/auth/login` | Login and get JWT | Public |
| GET | `/api/auth/me` | Get current user info | Authenticated |

### Properties

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/properties` | Browse catalog (public) | Public |
| GET | `/api/properties/{id}` | Get property details | Public |
| POST | `/api/properties` | Create property | Owner |
| PUT | `/api/properties/{id}` | Update property | Owner |
| DELETE | `/api/properties/{id}` | Delete property | Owner |
| POST | `/api/properties/{id}/availability` | Set availability | Owner |
| POST | `/api/properties/{id}/images` | Upload images | Owner |

### Bookings

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/bookings` | Create booking | Guest |
| GET | `/api/bookings` | List my bookings | Authenticated |
| PUT | `/api/bookings/{id}/confirm` | Confirm booking | Owner |
| PUT | `/api/bookings/{id}/cancel` | Cancel booking | Guest |
| PUT | `/api/bookings/{id}/complete` | Complete booking | Owner |
| GET | `/api/bookings/property/{id}` | Get property bookings | Owner |
| GET | `/api/bookings/availability` | Check availability | Public |

### KYC

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/kyc/documents` | Upload KYC document | Authenticated |
| GET | `/api/kyc/status` | Get KYC status | Authenticated |

### Wishlist

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/wishlist/{propertyId}` | Add to wishlist | Authenticated |
| DELETE | `/api/wishlist/{propertyId}` | Remove from wishlist | Authenticated |
| GET | `/api/wishlist` | Get wishlist | Authenticated |

### Notifications

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/notifications` | List notifications | Authenticated |
| PUT | `/api/notifications/{id}/read` | Mark as read | Authenticated |
| PUT | `/api/notifications/read-all` | Mark all as read | Authenticated |

### Dashboard & Reports

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/dashboard/occupancy` | Occupancy rate | Owner |
| GET | `/api/dashboard/revenue` | Revenue data | Owner |
| GET | `/api/dashboard/trends` | Booking trends | Owner |
| GET | `/api/reports/property/{id}` | Property report (.xlsx) | Owner |
| GET | `/api/reports/portfolio` | Portfolio report (.xlsx) | Owner |

---

## Module Descriptions

### Identity Module
User registration, login, JWT authentication, and role management (Guest, Owner). BCrypt password hashing, email normalization.

### Properties Module
Property CRUD with owner authorization. Public catalog with date/location/guest filters and pagination. Availability management and image uploads.

### Booking Module
Reservation lifecycle (Pending → Confirmed → Completed/Cancelled). SERIALIZABLE transaction with overlap formula prevents double bookings. KYC gate blocks first booking without identity verification.

### Wishlist Module
Guest favorites with idempotent add/remove. Composite unique constraint on (UserId, PropertyId, TenantId). Joins with property data for display.

### KYC Module
Identity verification with MinIO document storage (SSE encryption). Hangfire background processing with OCR stub. 90-day retention with automatic cleanup.

### Notifications Module
Domain event-driven notifications. Email via MailKit/Gmail SMTP. Hangfire retry (3 attempts, exponential backoff). In-app notification storage.

### Dashboard Module
Owner metrics: occupancy rate, revenue, booking trends. Date range filtering, property or portfolio scope.

### Reports Module
Excel (.xlsx) generation via ClosedXML. Single property or portfolio reports with date filtering.

---

## Testing

### Unit Tests (313)
- Entity behavior and validation
- Handler logic with mocked dependencies
- Controller authorization and responses
- Domain event handling
- Booking overlap formula (all edge cases)
- KYC gate logic
- Tenant isolation filters

### Integration Tests (27)
- **Double Booking Prevention** (8 tests): Same dates, partial overlap, full overlap, adjacent OK
- **Authorization & Tenant Isolation** (9 tests): Cross-tenant access blocked, role enforcement
- **KYC Workflow** (7 tests): First booking blocked, KYC upload, approval gate
- **End-to-End Workflows** (3 tests): Full happy path, cancel flow, wishlist flow

---

## License

Proprietary
