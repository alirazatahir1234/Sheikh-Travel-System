---
name: sheikhgo-go-live-priority
description: Production-readiness orchestrator for SheikhGo. Use when deciding what to build, fix, test, or deploy across Web/ERP, backend, Driver Mobile App, and MCP/AI. Prioritize Web/ERP + backend first, Driver Mobile App second, MCP/AI third. Focus on the critical end-to-end fleet workflow and avoid scope creep.
---

# SheikhGo Go-Live Priority

## Mission

Get SheikhGo to a reliable first production release.

The goal is not to complete every module. The goal is:

> One real customer can operate one fleet end-to-end without manual intervention from the development team.

## Priority Order

### P1 — Web / ERP + Backend

Prioritize:
- Authentication and authorization
- Company/tenant
- Users and roles
- Vehicles
- Drivers
- Trips/bookings
- Vehicle and driver assignment
- GPS/Traccar
- Live tracking
- Required maintenance/fuel workflows
- Required notifications
- Essential reports
- API reliability
- Database integrity
- Logging/error handling
- Security
- Production configuration
- Backups/recovery
- Deployment and health checks

### P2 — Driver Mobile App

Prioritize:
- Login/session
- Dashboard
- Assigned trips
- Trip details
- Accept/confirm where required
- Start trip
- Navigation where required
- Attendance/fuel where required
- Live GPS
- Notifications
- Complete trip
- Backend synchronization
- Network/offline/error handling for critical actions

### P3 — MCP / AI

MCP is a strategic differentiator, not a first-release blocker.

Continue MCP when it does not delay production blockers. Prefer useful, deterministic capabilities:
- Architecture understanding/review
- Code tracing/review
- Database/schema diagnosis
- Fleet/GPS context
- Test planning/failure analysis
- Security/DevOps inspection

Do not turn MCP into a browser ChatGPT clone or require it to be complete before go-live.

## Critical End-to-End Flow

Treat this as the main acceptance path:

```text
Company/Tenant
  -> Vehicle
  -> Driver
  -> Trip/Booking
  -> Vehicle Assignment
  -> Driver Assignment
  -> Driver receives trip
  -> Driver starts trip
  -> GPS tracking
  -> Admin sees live state
  -> Driver completes trip
  -> Backend persists completion
  -> ERP reflects completion
```

A task that unblocks or protects this flow takes priority over cosmetic or future work.

## Blocker Classification

### P0 — Must fix before go-live
Examples:
- Authentication/authorization failure
- Security vulnerability
- Data corruption
- Trip creation/assignment failure
- Driver cannot receive/start/complete a trip
- GPS tracking failure
- ERP/mobile state inconsistency
- Production API crash
- Unsafe secrets/configuration
- Critical deployment or recovery failure

### P1 — High priority
Examples:
- Important notification failures
- Unreliable synchronization
- Important error handling gaps
- Major operational reporting gaps
- Serious network/realtime problems
- Major performance problems

### P2 — Post-launch
Examples:
- Advanced dashboards
- Extra reports
- Advanced automation
- Non-critical UI polish
- Advanced AI capabilities
- Advanced MCP tools

### P3 — Future scope
Examples:
- Nice-to-have modules
- Experimental AI
- Large refactors without immediate production benefit
- Features without a real customer requirement

## Audit Before Editing

Before changing code:
1. Inspect repository structure.
2. Identify Web/ERP, backend, mobile, infrastructure, and MCP projects.
3. Identify the current architecture and existing APIs.
4. Inspect database schema/migrations.
5. Inspect authentication/authorization.
6. Inspect SignalR/GPS/Traccar integration.
7. Inspect current mobile workflows.
8. Inspect current MCP implementation.
9. Find the smallest set of files that needs changing.

Never assume something is missing until the repository is inspected.

## Architecture Preservation

Preserve the existing working architecture. SheikhGo may include:
- .NET / ASP.NET Core
- Clean Architecture
- CQRS / MediatR
- SQL Server
- Dapper/repository patterns where already used
- Angular
- Flutter
- SignalR
- Traccar
- GPS/telematics integrations
- MCP
- Optional LLM components

Rules:
- Do not rewrite working systems unnecessarily.
- Reuse existing services/components.
- Avoid unnecessary framework/package changes.
- Do not change API contracts without checking consumers.
- Do not change backend/mobile contracts without checking both sides.
- Prefer small, testable changes.

## Feature Triage

For every requested feature, ask:

1. Does it unblock the critical operational flow?
2. Does it prevent a production failure?
3. Does it protect data correctness?
4. Does it improve security?
5. Does it materially affect real customer operation?
6. Does it affect deployment/recovery?
7. Is it merely cosmetic or future scope?

If it is not a go-live requirement, state why it is not a blocker and place it in P2/P3 instead of allowing scope creep.

## Web/ERP Definition of Done

Verify:
- Company/tenant works
- Users/roles work
- Vehicles work
- Drivers work
- Trips/bookings work
- Assignment works
- Trip state transitions are correct
- GPS state is visible
- Completion persists
- Required notifications work
- Authentication/authorization are enforced
- Tenant isolation is verified
- Secrets are protected
- Input validation exists
- Production debug settings are disabled
- API/database/GPS failures are handled
- Logging is useful
- Production configuration is documented
- Database migrations are controlled
- HTTPS is configured
- Backup/recovery procedure exists

## Driver App Definition of Done

Verify:

```text
Login
 -> Dashboard
 -> Assigned Trip
 -> Trip Details
 -> Accept/Confirm
 -> Start Trip
 -> GPS Tracking
 -> Required Operational Actions
 -> Complete Trip
 -> Backend Confirmation
```

Also verify:
- Token/session handling
- 401 handling
- Network loss/timeouts
- GPS permission handling
- Background location where required
- Duplicate requests
- App restart during active trip
- Realtime behavior where used
- Local state/cache consistency
- Production API configuration

Do not prioritize animations or cosmetic redesign while this workflow has defects.

## MCP Rules

MCP should support the product without becoming the release blocker.

Prefer early capabilities for:
- Architecture
- Code
- Database
- Fleet/GPS
- Testing
- DevOps/security

If the MCP Console prepares requests for Cursor/Claude rather than executing them, preserve that transparency. Never claim execution unless the backend actually reports execution.

## Testing Priority

1. Critical end-to-end flow
2. Authentication/authorization/tenant isolation
3. Trip state transitions
4. Driver assignment
5. GPS updates
6. Completion/database persistence
7. API error handling
8. Secondary modules
9. UI edge cases/performance
10. Experimental MCP/AI features

## Change Process

For every implementation:
1. Audit what exists.
2. Identify impact across backend/web/mobile/database/config/tests.
3. Create the smallest implementation plan.
4. Implement only necessary changes.
5. Run relevant builds/tests.
6. Verify the critical workflow and regression risk.
7. Report changes, files, tests, remaining risks, and priority.

## Anti-Scope-Creep Rules

Do NOT:
- Rebuild the whole application without a reason.
- Replace architecture unnecessarily.
- Add unrelated packages.
- Redesign unrelated screens.
- Add fake data or invented business requirements.
- Invent customers, pricing, integrations, or claims.
- Add AI merely because it is possible.
- Delay go-live for MCP polish.
- Delay go-live for non-critical UI polish.
- Remove existing functionality without approval.
- Deploy/publish production automatically unless explicitly requested.

## Go-Live Assessment

When asked for readiness, report:

```text
GO-LIVE STATUS

Web/ERP:       READY / BLOCKED / IN PROGRESS
Backend:       READY / BLOCKED / IN PROGRESS
Driver App:    READY / BLOCKED / IN PROGRESS
GPS/Traccar:   READY / BLOCKED / IN PROGRESS
Database:      READY / BLOCKED / IN PROGRESS
Security:      READY / BLOCKED / IN PROGRESS
Deployment:    READY / BLOCKED / IN PROGRESS
MCP/AI:        READY / BLOCKED / IN PROGRESS
```

Then list:
- Blocking Issues
- Pre-Launch Tasks
- Post-Launch Tasks
- MCP Backlog

Do not use an arbitrary percentage score.

## Default Work Allocation

Before the first production customer, use this as a planning guide:

- 60% Web/ERP + Backend
- 30% Driver Mobile App
- 10% MCP

These are not rigid rules. A P0 production issue always takes priority.

## Cursor Response Format

When asked what to work on next:

```text
## Current Priority
[Web / Backend / Mobile / MCP]

## Why
[Short factual reason]

## Priority
P0 / P1 / P2 / P3

## Current Blocker
[What is preventing the next milestone]

## Recommended Action
[Smallest concrete next step]

## Do Not Work On Yet
[Lower-priority distractions]

## Validation
[How completion will be verified]
```

When asked to implement:

```text
## Audit
[What exists]

## Goal
[Specific production outcome]

## Files / Modules
[Exact affected areas]

## Plan
1. ...
2. ...
3. ...

## Implementation
[Make changes]

## Verification
- Build:
- Tests:
- Critical flow:
- Regression check:

## Result
[What works now]

## Remaining Blockers
[Only real blockers]
```

## Golden Rule

Optimize for:

> A reliable first customer, not a perfect platform.

The release sequence is:

```text
Web / ERP + Backend
        ↓
Driver Mobile App
        ↓
GPS / Traccar Validation
        ↓
Production Hardening
        ↓
First Customer
        ↓
MCP / AI Expansion
```

Never allow a non-critical MCP, AI, UI, or future-feature task to silently become a blocker for the first production release.
