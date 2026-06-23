---
type: business
status: draft
---

# Business

> Created by `kaddo bootstrap`. The minimal **why** of the project. Refine with the
> business-agent. As this matures it can split into problem.md, users.md, …

## Problem

Property owners and accommodation managers need a reliable way to publish, manage and monetize their properties while avoiding operational issues such as reservation conflicts, poor visibility into performance, manual communication processes and insecure identity verification.

Guests need a trusted experience where they can discover properties, make reservations confidently and have their personal information protected.

Current processes often require fragmented tools for:

- Property management.
- Availability control.
- Reservation tracking.
- Customer communication.
- Revenue analysis.
- Identity validation.

The product solves the need for a centralized platform that connects property availability, booking workflows and operational management.

---

## Users

### Guests / Customers

Users who want to:

- Search and review available properties.
- Save properties for future decisions.
- Book accommodations easily.
- Receive reservation updates.
- Complete identity verification securely.

Their main goal is to find and reserve suitable properties with confidence.

---

### Property Owners / Managers

Users who want to:

- Publish and manage their properties.
- Control availability.
- Avoid double bookings.
- Monitor occupancy and revenue.
- Understand property performance.
- Manage guest interactions.

Their main goal is to maximize property utilization while reducing operational complexity.

---

### Platform Administrators

Users who want to:

- Manage users and permissions.
- Monitor platform activity.
- Maintain operational security.
- Ensure business rules are correctly enforced.

Their main goal is to keep the platform reliable and compliant.

---

## Value Proposition

The platform provides a unified property management and booking experience that:

- Reduces manual management effort.
- Prevents reservation conflicts.
- Improves property visibility.
- Provides business insights through dashboards and reports.
- Protects sensitive customer information.
- Enables secure and scalable operations.

For owners:

> Increase occupancy and control operations from a single platform.

For guests:

> Find and reserve properties through a secure and reliable experience.

---

## Business Rules

- A property cannot have overlapping confirmed reservations.

- Availability must always be validated on the backend before confirming a reservation.

- Reservation dates must follow standardized check-in and check-out rules.

- Only authenticated users can create reservations.

- Users can only access information allowed by their permissions.

- Property owners can only manage properties assigned to them.

- KYC documents must be stored securely and removed according to retention policies.

- Sensitive identity information must not be publicly accessible.

- Notifications must be generated for relevant booking lifecycle events.

- Reports must reflect accurate booking and revenue information.

---

## Constraints

- The solution must be implemented strictly using .NET technologies.

- The product must be delivered under a limited implementation window.

- The architecture must balance maintainability and development speed.

- Sensitive user documents require encryption and protected storage.

- Booking operations require transactional consistency.

- Background processing is required for asynchronous operations such as:
  - Notifications.
  - Emails.
  - Document processing.
  - Cleanup tasks.

- The system must support future scalability without requiring a complete redesign.

---

## Assumptions

- External services can be used for specialized capabilities such as OCR/KYC validation.

- Users have access to modern web browsers.

- Property owners provide accurate availability and property information.

- Payment processing can be integrated through external providers.

- The first release prioritizes core booking and management capabilities over advanced automation.

- A modular architecture is sufficient before extracting independent services.

---

## Open Questions

- Which payment provider will be integrated?

- What are the exact KYC retention periods required by regulations?

- Which external OCR/AI provider will be used?

- What are the final user roles and permission levels?

- Are there tenant-specific customization requirements?

- What reporting metrics are considered mandatory for business operations?

- What are the expected traffic volumes and scalability targets?

---

## Quality checklist

- [x] The problem is stated without assuming the solution.
- [x] Users have goals, not just labels.