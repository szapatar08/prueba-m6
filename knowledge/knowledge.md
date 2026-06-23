---
type: current-state
updated_at: 2026-06-23
---

# prueba — Knowledge

> What is true about this product right now.

## Purpose

The product is a multi-tenant property management and booking platform designed to connect users looking for accommodations with property owners who need to manage listings, reservations, profitability and customer interactions.

The platform serves two main audiences:

- **Guests / Customers**
  - Browse available properties.
  - Manage favorites.
  - Create reservations.
  - Receive booking notifications.
  - Complete identity verification (KYC).

- **Property Owners / Administrators**
  - Publish and manage properties.
  - Control availability.
  - Manage reservations.
  - Monitor occupancy and revenue.
  - Export business reports.

The system prioritizes secure booking management, prevention of reservation conflicts, identity validation and operational visibility.

---

## Architecture overview

The system is implemented as a **Modular Monolith using .NET**, optimized for rapid delivery while maintaining scalability and maintainability.

Main technology decisions:

- Backend:
  - ASP.NET Core (.NET)
  - Clean Architecture principles
  - Vertical Slice organization
  - Entity Framework Core

- Frontend:
  - React application
  - Consumes REST APIs
  - JWT-based authentication

- Data and infrastructure:
  - PostgreSQL as primary database
  - Redis for optional caching
  - MinIO/object storage for private documents
  - Hangfire for background processing

High-level components:
    React Frontend
    |
    |
    ASP.NET Core API
    |
    |
    | Identity Module |
    | Properties Module |
    | Booking Module |
    | Wishlist Module |
    | KYC Module |
    | Notification Module |
    | Dashboard Module |
    | Reporting Module |
    |
    |PostgreSQL + Object Storage
    |
    Background Workers


The architecture intentionally avoids distributed microservices due to delivery constraints while keeping clear domain boundaries that allow future extraction into independent services.

---

## Key domains

### Identity

Responsible for:

- User registration.
- Authentication.
- Authorization.
- Role management.
- JWT tokens.

Main entities:

- User
- Role
- Permission

---

### Properties

Responsible for property lifecycle management.

Capabilities:

- Create and update properties.
- Manage availability.
- Store property information.
- Manage owner relationships.

Main entities:

- Property
- PropertyImage
- Availability

---

### Booking

Core business domain responsible for reservations.

Capabilities:

- Create reservations.
- Validate availability.
- Prevent overlapping bookings.
- Manage booking status.

Important business rule:

A property cannot have overlapping confirmed reservations.

Main entities:

- Booking
- ReservationStatus
- Payment

---

### Wishlist

Allows users to save properties for future review.

Capabilities:

- Add favorites.
- Remove favorites.
- Retrieve saved properties.

Main entities:

- WishlistItem

---

### KYC / Identity Verification

Responsible for customer identity validation.

Capabilities:

- Upload identity documents.
- Process documents using OCR/AI services.
- Validate extracted information.
- Securely store verification results.

Security requirements:

- Private storage.
- Encryption.
- Automatic document cleanup.

Main entities:

- KycValidation
- KycDocument

---

### Notifications

Responsible for communication workflows.

Capabilities:

- Booking confirmations.
- Status changes.
- User notifications.
- Email delivery.

Implementation approach:

- Domain events.
- Background processing.

Main entities:

- Notification
- NotificationTemplate

---

### Dashboard and Reporting

Provides business insights for property owners.

Capabilities:

- Occupancy metrics.
- Revenue calculations.
- Property performance.
- Excel exports.

Main metrics:

- Occupancy rate.
- Revenue.
- Booking trends.

---

## Active constraints

### Delivery constraint

The product must be delivered in less than 6 hours.

Impact:

- Prefer simple operational architecture.
- Avoid unnecessary distributed infrastructure.
- Use proven frameworks.
- Prioritize core business functionality.

---

### Technology constraint

The project must use .NET strictly.

Current decisions:

- ASP.NET Core backend.
- Entity Framework Core.
- .NET ecosystem libraries.

Laravel is not part of the implementation.

---

### Architecture constraint

The solution must be maintainable but fast to implement.

Chosen approach:

- Modular Monolith.
- Clean Architecture concepts.
- Domain-oriented modules.

Avoided:

- Full microservices.
- Complex event infrastructure.
- Excessive abstractions.

---

### Security constraint

Sensitive information requires protection.

Controls:

- JWT authentication.
- Role-based access control.
- Encrypted KYC documents.
- Private object storage.
- Automatic document deletion.

---

### Data consistency constraint

Reservations require strong consistency.

Rules:

- Prevent double booking.
- Validate availability server-side.
- Use database transactions for critical operations.

---

### Operational constraint

Background tasks are required for:

- Emails.
- Notifications.
- KYC processing.
- Cleanup jobs.

Implemented through background workers.

---

### Scalability constraint

The current design supports future evolution:

Possible future extraction:

- Identity Service.
- Booking Service.
- Notification Service.
- Reporting Service.

The current architecture keeps these boundaries explicit to allow migration without rewriting the product.