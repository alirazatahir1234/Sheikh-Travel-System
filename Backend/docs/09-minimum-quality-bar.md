# Minimum Quality Bar Before Merge

A change is ready only if:

* it builds successfully
* it passes tests
* it follows the project architecture
* it is reviewed and approved
* it is safe to deploy
* it is documented if needed

---

## Backend (.NET) quality checks

```bash
# Must all pass before merge
dotnet build --no-restore          # Zero errors, zero warnings
dotnet test --no-build             # All tests green
```

| Check | What to verify | Example |
|-------|---------------|---------|
| Build | `dotnet build` passes | No compile errors |
| Tests | `dotnet test` passes | xUnit tests for handlers and validators |
| Architecture | Clean Architecture layers respected | No business logic in controllers |
| CQRS | Commands/Queries have handlers | `CreateBookingCommand` → `CreateBookingHandler` |
| Validation | FluentValidation rules defined | `CreateBookingCommandValidator` exists |
| SQL safety | All queries parameterized | `WHERE Id = @Id` not `WHERE Id = {id}` |
| Auth | Endpoints protected | `[Authorize(Roles = "Admin")]` |
| DTOs | No entity exposure | `BookingResponse` returned, not `Booking` entity |
| Logging | Business events logged | `_logger.LogInformation(...)` in handlers |
| No debug code | No `Console.WriteLine` | Clean production-ready code |
| No secrets | Config via `appsettings` | No hardcoded connection strings |

---

## Frontend (Angular) quality checks

```bash
# Must all pass before merge
ng build --configuration production    # Zero errors
ng test --watch=false --browsers=ChromeHeadless   # All tests green
ng lint                                # Zero lint errors
```

| Check | What to verify | Example |
|-------|---------------|---------|
| Build | `ng build` passes | No TypeScript errors |
| Tests | `ng test` passes | Jasmine tests for components and services |
| Lint | `ng lint` passes | ESLint rules satisfied |
| Typed models | No `any` types | `Booking` interface, not `any` |
| Standalone | Components are standalone | `standalone: true` |
| Services | HTTP calls in services | `BookingService`, not `HttpClient` in component |
| Validation | Reactive form validators | `Validators.required`, `Validators.maxLength(100)` |
| Guards | Protected routes have guards | `canActivate: [authGuard]` |
| Environment | API URLs from environment | `environment.apiUrl`, not hardcoded strings |
| Cleanup | No `console.log` | Clean production-ready code |
| Unsubscribe | No subscription leaks | `takeUntilDestroyed` or `async` pipe |
