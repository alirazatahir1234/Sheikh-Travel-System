# Update Pull Request Guidelines

## Goal

When a PR needs changes after review, update it cleanly and intentionally.

## Rules for updating a PR

* Do not create a new PR for the same work unless the branch strategy requires it.
* Address review comments one by one.
* Add a response to each important comment explaining what was changed.
* Re-run tests after updates.
* Push only the necessary code changes.

---

## How to respond to review comments

For each important comment, respond with:

* what changed
* why it changed
* whether any trade-off remains

---

**Example — Backend (.NET) review responses:**

| Reviewer comment | Your response |
|-----------------|---------------|
| "Move price calculation out of the controller" | "Moved to `CalculateBookingPriceHandler`. Controller now only calls `_mediator.Send(command)`." |
| "This query uses `SELECT *` — limit to needed columns" | "Updated SQL to `SELECT Id, PassengerName, TotalPrice, Status FROM Bookings`." |
| "Missing validation for zero passenger count" | "Added `RuleFor(x => x.PassengerCount).GreaterThan(0)` in `CreateBookingCommandValidator`." |
| "No `[Authorize]` on the delete endpoint" | "Added `[Authorize(Roles = \"Admin\")]` to `Cancel` action." |
| "Handler is 90 lines — too long" | "Extracted `BuildInsertSql()` and `MapToResponse()` as private methods." |

**Example — Frontend (Angular) review responses:**

| Reviewer comment | Your response |
|-----------------|---------------|
| "Component calls HttpClient directly" | "Moved HTTP call to `BookingService.create()`. Component now calls the service." |
| "No error handling on the form submit" | "Added `.subscribe({ error })` block that sets `this.errorMessage` and shows a snackbar." |
| "Using `any` for the booking model" | "Created `Booking` interface in `models/booking.model.ts`. All usages are now typed." |
| "No unsubscribe — potential memory leak" | "Added `takeUntilDestroyed(this.destroyRef)` to the subscription in `ngOnInit`." |
| "Form validation message not showing" | "Added `@if (form.controls.passengerName.hasError('required'))` block under the input." |

---

## When updating a PR, check again

### Backend (.NET)

```bash
dotnet build
dotnet test
```

Verify:

* no new compile errors or warnings
* all existing tests still pass
* new tests pass for the changed code
* the original review issue is fixed
* no new side effects introduced

### Frontend (Angular)

```bash
ng build
ng test
ng lint
```

Verify:

* no TypeScript compile errors
* all Jasmine/Karma tests pass
* ESLint passes with no new warnings
* the original review issue is fixed
* no regressions in related components

---

## Good PR update behavior

* Keep the same branch unless instructed otherwise.
* Do not make unrelated refactors during review fixups.
* Avoid repeatedly force-pushing large unrelated changes.
* Keep the conversation in the PR clear and professional.

**Example — Focused commit messages for review fixes:**

```
fix: move price calculation from controller to handler
fix: add passenger count validation in CreateBookingCommandValidator
fix: replace SELECT * with explicit column list in GetBookingsQuery
fix: add Authorize attribute to Cancel endpoint
fix: replace any types with Booking interface in booking-list component
fix: add takeUntilDestroyed to prevent subscription leak
```
