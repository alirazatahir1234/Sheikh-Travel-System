# Create Pull Request Guidelines

## Goal

A pull request should be created only when the feature or fix is ready for review.

## When to create a PR

Create a PR when:

* the feature is complete
* unit tests are added or updated
* the code builds successfully
* the work is limited to one logical task
* no debug code is left behind

---

## PR title format

Use a clear and short title that describes the change.

**Backend (.NET) examples:**

* `Add booking creation CQRS handler and endpoint`
* `Implement vehicle pricing calculation with Dapper`
* `Fix route validation — reject zero-distance routes`
* `Add JWT role-based authorization to admin endpoints`

**Frontend (Angular) examples:**

* `Add booking form with reactive validation`
* `Implement vehicle list with pagination and search`
* `Fix route selector — handle empty routes gracefully`
* `Add auth guard to protect dashboard routes`

---

## PR description format

Every PR should include:

### Summary

What was changed and why.

### Scope

Which modules, files, or endpoints were affected.

### Testing

How the change was tested.

### Notes

Any assumptions, edge cases, or follow-up work.

### Screenshots or examples

If the change affects UI, API output, or behavior, include examples.

---

**Example — Backend PR description:**

```
### Summary
Added the Create Booking feature. A new CQRS command/handler persists a booking
using Dapper. FluentValidation validates the request payload. The endpoint
returns 201 with the created booking ID.

### Scope
- API: BookingsController.cs — POST /api/bookings
- Application: CreateBookingCommand.cs, CreateBookingHandler.cs, CreateBookingCommandValidator.cs
- Infrastructure: BookingRepository.cs (Dapper SQL)
- DTOs: CreateBookingRequest.cs, BookingCreatedResponse.cs

### Testing
- Added xUnit tests for CreateBookingHandler (success + validation failure)
- Added xUnit tests for CreateBookingCommandValidator
- Tested manually via Postman with valid and invalid payloads

### Notes
- Pricing calculation is not included in this PR — will be a separate feature
- Assumes RouteId and VehicleId are valid (FK constraint enforced at DB level)
```

**Example — Frontend PR description:**

```
### Summary
Implemented the booking creation form using reactive forms with validation.
On successful submission, the user is redirected to the booking detail page.

### Scope
- Components: BookingFormComponent (standalone)
- Services: BookingService — added create() method
- Models: CreateBookingRequest interface
- Routing: Added /bookings/new route

### Testing
- Added Jasmine tests for BookingFormComponent (form validity, submit behavior)
- Added Jasmine tests for BookingService.create()
- Manual testing in Chrome and Firefox

### Notes
- Vehicle dropdown loads from GET /api/vehicles — assumes the endpoint is ready
- Date picker uses Angular Material DatePicker
```

---

## PR size rules

* Keep PRs small and focused.
* Avoid mixing unrelated work.
* One PR should ideally solve one problem or one feature.
* Large PRs should be split into multiple smaller PRs.

**Splitting example for a large "Booking" feature:**

| PR | Scope |
|----|-------|
| PR 1 | Backend: `CreateBookingCommand`, handler, validator, endpoint |
| PR 2 | Backend: `GetBookingsQuery` with pagination |
| PR 3 | Frontend: Booking form component with validation |
| PR 4 | Frontend: Booking list page with pagination |

---

## PR branch naming

Use a consistent format:

**Backend:**

* `feature/backend-create-booking`
* `feature/backend-vehicle-pricing`
* `bugfix/backend-route-validation`
* `hotfix/backend-jwt-token-expiry`

**Frontend:**

* `feature/frontend-booking-form`
* `feature/frontend-vehicle-list`
* `bugfix/frontend-route-selector`
* `hotfix/frontend-login-redirect`

---

## Before creating the PR

### Backend (.NET) checklist

* `dotnet build` succeeds with no errors
* `dotnet test` passes all tests
* No `Console.WriteLine` or debug breakpoints left
* No hardcoded connection strings or secrets (use `appsettings.json` + user secrets)
* No `// TODO` or commented-out code blocks
* FluentValidation rules are defined for all new commands/queries

### Frontend (Angular) checklist

* `ng build` succeeds with no errors
* `ng test` passes all tests
* `ng lint` passes (ESLint)
* No `console.log` statements left
* No hardcoded API URLs (use `environment.ts`)
* No `any` types — use proper interfaces/models
* All new components are standalone
