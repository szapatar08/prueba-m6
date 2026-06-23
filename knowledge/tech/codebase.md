---
type: codebase
status: draft
---

# Codebase

> Created by `kaddo bootstrap`. The minimal **how** of the project. Refine with the
> codebase-agent. It describes the intended base — it does **not** generate code.

## Repository Structure

The repository follows a domain-oriented modular monolith structure instead of a framework-first organization.

Recommended structure:
/

├── src/
│
│ ├── Api/
│ │ └── ASP.NET Core API entrypoint
│ │
│ ├── Application/
│ │ ├── Use cases
│ │ ├── DTOs
│ │ ├── Validators
│ │ └── Business workflows
│ │
│ ├── Domain/
│ │ ├── Entities
│ │ ├── Value Objects
│ │ ├── Domain rules
│ │ └── Events
│ │
│ ├── Infrastructure/
│ │ ├── Database
│ │ ├── External services
│ │ ├── Storage
│ │ └── Background jobs
│ │
│ └── Modules/
│ │
│ ├── Identity/
│ ├── Properties/
│ ├── Booking/
│ ├── Wishlist/
│ ├── KYC/
│ ├── Notifications/
│ ├── Dashboard/
│ └── Reports/
│
├── tests/
│ ├── UnitTests/
│ └── IntegrationTests/
│
├── frontend/
│ └── React application
│
├── docker/
│
├── docs/
│
└── README.md


The structure follows business capabilities to keep boundaries clear and allow future extraction into independent services.

---

## Candidate Stack

### Backend

- ASP.NET Core (.NET)
- C#
- Entity Framework Core
- REST API
- JWT Authentication
- FluentValidation
- Dependency Injection

---

### Frontend

- React
- TypeScript
- React Router
- API client layer
- Component-based UI architecture

---

### Database

- PostgreSQL

Used for:

- Users.
- Properties.
- Reservations.
- Notifications.
- Reports data.

---

### Infrastructure

- Docker / Docker Compose
- Redis (optional caching)
- MinIO object storage
- Hangfire background processing

---

### External Integrations

- OCR/KYC provider
- Email provider
- Payment provider (future)

---

## Quality Attributes

### Maintainability

Achieved through:

- Modular design.
- Clear domain boundaries.
- Separation of concerns.
- Business rules isolated from infrastructure.

---

### Security

Requirements:

- JWT authentication.
- Role-based authorization.
- Protected storage for sensitive documents.
- Encryption for KYC information.
- Server-side validation.

---

### Reliability

Important guarantees:

- No overlapping confirmed reservations.
- Transactional booking operations.
- Background jobs with retry mechanisms.

---

### Scalability

Prepared for:

- Module extraction.
- Horizontal API scaling.
- Independent service deployment in the future.

---

### Performance

Considerations:

- Database indexing.
- Optimized queries.
- Optional caching.
- Asynchronous processing.

---

## Development Standards

### Code Organization

- Organize by domain capability.
- Avoid infrastructure dependencies in business logic.
- Keep modules independent.

---

### Coding Practices

- Follow clean code principles.
- Prefer explicit business rules.
- Use meaningful names.
- Avoid unnecessary abstractions.

---

### API Standards

- REST conventions.
- Consistent HTTP responses.
- DTO contracts.
- Validation before processing.

---

### Testing Standards

Expected coverage:

- Unit tests for business rules.
- Integration tests for API flows.
- Critical tests for booking availability.

---

### Documentation

Maintain:

- Architecture decisions.
- Setup instructions.
- Environment configuration.
- API documentation.

---

## Git Strategy

GitHub Flow + Conventional Commits + SemVer (default). See `kaddo add git-strategy`.

Recommended branches:
main
|
└── feature/*
└── fix/*
└── hotfix/*


Commit examples:
feat: add booking validation
fix: prevent overlapping reservations
docs: update architecture notes


---

## Initial Modules

### Identity

Responsible for:

- Authentication.
- Authorization.
- Users.
- Roles.

---

### Properties

Responsible for:

- Property catalog.
- Property ownership.
- Availability data.

---

### Booking

Responsible for:

- Reservation lifecycle.
- Availability validation.
- Double booking prevention.

---

### Wishlist

Responsible for:

- Favorite properties.
- Saved searches.

---

### KYC

Responsible for:

- Identity documents.
- Verification workflow.
- Secure storage.

---

### Notifications

Responsible for:

- Emails.
- In-app notifications.
- Event handling.

---

### Dashboard

Responsible for:

- Owner metrics.
- Occupancy.
- Revenue indicators.

---

### Reports

Responsible for:

- Data exports.
- Operational reports.

---

## Assumptions

- The first version is delivered as a modular monolith.
- PostgreSQL can support initial business scale.
- External providers handle specialized services.
- React remains independent from backend implementation.
- Modules will evolve independently as the product grows.

---

## Open Questions

- Should tenant isolation use shared database with TenantId or separate schemas?
- Which cloud provider will host the platform?
- Which CI/CD pipeline will be used?
- Which monitoring and logging tools will be adopted?
- Which payment provider will be integrated?
- What are the expected production traffic levels?

---

## Quality checklist

- [x] Structure follows business and product, not a framework default.
- [x] No production code is described here — only the foundation.