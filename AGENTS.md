# AGENTS.md

# Project Context

You are an AI software engineering agent working on a property management and booking platform.

Your responsibility is to maintain a secure, scalable, maintainable and production-ready codebase.

The system is a multi-tenant platform where property owners manage accommodations and guests can search, reserve and interact with properties.

The project must be implemented strictly using the .NET ecosystem.

---

# Core Product Understanding

The platform provides:

- Property management.
- Property availability management.
- Guest reservations.
- Booking lifecycle management.
- Wishlist functionality.
- User authentication.
- Role management.
- KYC identity verification.
- Notifications.
- Owner dashboards.
- Reports and exports.

The system serves:

## Guests

Goals:

- Find available properties.
- Make reservations safely.
- Manage favorites.
- Receive booking updates.
- Complete identity verification securely.

## Property Owners

Goals:

- Publish properties.
- Manage availability.
- Prevent double bookings.
- Track occupancy.
- Track revenue.
- Manage operations.

## Administrators

Goals:

- Manage platform integrity.
- Control permissions.
- Monitor security.

---

# Architecture Rules

The system follows:

- Modular Monolith architecture.
- Clean Architecture principles.
- Domain-driven organization.
- Vertical Slice development style.

Do not introduce:

- Unnecessary microservices.
- Distributed complexity.
- Framework-driven folder structures.
- Tight coupling between modules.

The architecture should allow future extraction into services.

---

# Technology Constraints

Backend:

Required:

- ASP.NET Core
- C#
- Entity Framework Core
- PostgreSQL
- REST API
- JWT authentication

Frontend:

- React
- TypeScript

Infrastructure:

- Docker
- Docker Compose
- Redis (optional)
- MinIO/object storage
- Hangfire/background processing

Avoid:

- Laravel
- PHP backend
- Alternative backend frameworks

---

# Repository Organization

Expected structure:

src/

    Api/

    Application/

    Domain/

    Infrastructure/

    Modules/

        Identity/

        Properties/

        Booking/

        Wishlist/

        KYC/

        Notifications/

        Dashboard/

        Reports/


frontend/

tests/

docs/


Business modules must own their logic.

Do not place business rules in controllers.

---

# Domain Rules

## Booking

Critical business area.

Never allow:

- Double booking.
- Overlapping confirmed reservations.
- Client-controlled availability validation.

Availability must always be checked server-side.

Required validation:

Existing booking:

StartDate < NewEndDate

AND

EndDate > NewStartDate


Booking confirmation must use transactional consistency.

---

# Multi-Tenant Rules

All tenant-owned data must respect isolation.

Rules:

- Every tenant entity must have TenantId.
- Queries must filter by tenant.
- Never expose data across tenants.
- Authorization must validate tenant ownership.

Example:

Bad:
GetAllProperties()

Good:
GetPropertiesByTenant(currentTenantId)


Never trust tenant identifiers from the frontend.

Tenant context must come from authenticated identity.

---

# Security Rules

Security is mandatory.

Never:

- Store passwords manually.
- Expose sensitive documents.
- Return internal exceptions to users.
- Trust frontend validation.

Always:

- Validate authorization server-side.
- Use secure authentication.
- Protect sensitive data.
- Sanitize inputs.


---

# KYC / Identity Verification

KYC documents are sensitive.

Requirements:

- Store documents privately.
- Encrypt sensitive files.
- Use external OCR/AI services.
- Remove temporary files automatically.

Never:

- Store documents in public folders.
- Log document contents.
- Return raw identity data unnecessarily.


---

# API Rules

Controllers should be thin.

Controllers:

Allowed:

- Receive requests.
- Validate basic input.
- Call application layer.
- Return responses.


Not allowed:

- Business logic.
- Database queries directly.
- Complex calculations.


---

# Application Layer Rules

Contains:

- Use cases.
- Commands.
- Queries.
- DTOs.
- Validators.

Responsibilities:

- Coordinate business operations.
- Apply application rules.

Avoid:

- Direct infrastructure coupling.

---

# Domain Layer Rules

Contains:

- Entities.
- Value objects.
- Aggregates.
- Domain events.
- Business rules.


Domain should not depend on:

- EF Core.
- Controllers.
- External APIs.

---

# Infrastructure Rules

Contains:

- Database implementation.
- External providers.
- Storage.
- Background jobs.


Infrastructure can depend on:

- Application.
- Domain.


Domain cannot depend on infrastructure.

---

# React Rules

Frontend responsibilities:

- User experience.
- Presentation.
- API communication.

Do not duplicate backend rules.

Avoid:

- Booking validation only in frontend.
- Security assumptions.
- Hardcoded permissions.

Components should be:

- Reusable.
- Small.
- Focused.

---

# Notifications

Use asynchronous processing.

Examples:

- Booking confirmed.
- Booking cancelled.
- KYC completed.


Preferred approach:

Domain event

↓

Handler

↓

Notification service


Avoid blocking API requests.

---

# Background Jobs

Use background processing for:

- Emails.
- Notifications.
- KYC processing.
- Cleanup tasks.


Jobs must:

- Be retryable.
- Handle failures.
- Be idempotent.

---

# Database Rules

Use:

- EF Core migrations.
- Proper indexes.
- Foreign keys.
- Transactions when required.


Important indexes:

Booking:

- PropertyId
- StartDate
- EndDate
- Status


Avoid:

- N+1 queries.
- Unbounded queries.
- Loading unnecessary relations.

---

# Code Quality Rules

Prefer:

- Clean code.
- SOLID principles.
- Simple solutions.
- Explicit behavior.

Avoid:

- Overengineering.
- Premature abstractions.
- Huge classes.
- God objects.

---

# Testing Rules

Critical flows require tests:

Must test:

- Booking conflicts.
- Tenant isolation.
- Authorization.
- KYC workflow.
- Property ownership rules.


Tests should verify business behavior.

---

# Git Rules

Use:

GitHub Flow

Commit format:

Conventional Commits


Examples:

feat:
fix:
refactor:
docs:
test:
chore:


---

# Review Checklist

Before approving changes verify:

## Architecture

[ ] Module boundaries respected.

[ ] No domain leakage.

[ ] No unnecessary complexity.


## Security

[ ] Authorization exists.

[ ] Sensitive data protected.

[ ] Tenant isolation enforced.


## Booking

[ ] No double booking possible.

[ ] Server validates availability.


## Quality

[ ] Code is maintainable.

[ ] Tests exist for critical rules.

[ ] Errors are handled correctly.


---

# Decision Priority

When choosing between options:

1. Security
2. Business correctness
3. Maintainability
4. Simplicity
5. Performance optimization

The fastest implementation is not always the correct implementation.

Prefer solutions that can evolve.