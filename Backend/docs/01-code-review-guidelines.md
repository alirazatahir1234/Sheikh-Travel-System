# Code Review Guidelines

## Goal

Code review is done to ensure:

* correctness
* readability
* maintainability
* security
* performance
* consistent architecture
* production readiness

---

## Pull request workflow

Code review is a two-sided process. Authors prepare; reviewers verify. Both sides share responsibility for quality.

### Author responsibilities (before opening a PR)

* Self-review the diff — read it as if you did not write it.
* Link the ticket or requirement; explain **what** changed and **why** in the PR description.
* For UI changes, attach screenshots or a short screen recording.
* Confirm CI is green locally (build, lint, tests) before requesting review.
* Keep the PR **small and focused** — one logical change per PR when possible.
* Resolve or reply to every review comment before merge.
* Do not merge your own PR unless project policy explicitly allows it.

### PR size

Small, focused PRs are the single biggest lever on review quality.

| Guideline | Target |
|-----------|--------|
| Ideal size | ≤ 400 lines changed (excluding generated files) |
| Maximum without split discussion | ~800 lines |
| When to split | Unrelated refactors, multiple features, schema + UI in one shot |

**Must-fix:** A PR that mixes unrelated concerns (e.g. fleet schema migration + driver UI redesign) — ask the author to split.

### CI precondition

Human review starts **after** automated checks pass.

* Build must succeed (.NET `dotnet build`, Angular `ng build`).
* Lint / static analysis must pass.
* Existing tests must pass.
* Reviewers should not spend time on issues CI can catch (formatting, compile errors, broken tests).

If CI is red, mark the PR **blocked** and do not approve until green.

### Approvals, ownership, and turnaround

* Follow **CODEOWNERS** (or team routing) for modules you do not own — get the right reviewer.
* **One approval** is sufficient for routine changes; **two approvals** for money-touching flows, auth, schema migrations, or breaking API changes (team lead discretion).
* Target review turnaround: **within one business day** for normal PRs; same-day for hotfixes.
* **Approve** — you are satisfied; remaining nice-to-have comments are optional follow-ups.
* **Request changes** — one or more **must-fix** items remain; merge is blocked until resolved.
* **Comment without approval** — feedback only; author may merge if they have another approver and no must-fix items from you.

### Disagreement resolution

1. Author and reviewer discuss in the PR thread with concrete reasoning.
2. If still blocked after one round, escalate to the **tech lead** or module owner.
3. For architectural forks, schedule a **short sync** (15 min) rather than long comment threads.
4. Document the decision in the PR so future readers understand the trade-off.

### Severity labels are mechanical

| Label | Merge rule |
|-------|------------|
| **Must-fix** | **Blocks merge** until resolved or explicitly withdrawn by reviewer |
| **Should-fix** | Does not block merge; author should fix or reply with rationale |
| **Nice-to-have** | Never blocks merge; optional follow-up ticket is acceptable |

---

## What reviewers should check

### Functional correctness

* Does the code do what the ticket or requirement asks?
* Are edge cases handled?
* Are validations in place?
* Are failure cases covered?

**Example — .NET: Handler must cover the "not found" case**

```csharp
// GOOD — handles missing entity
public async Task<BookingResponse?> Handle(GetBookingByIdQuery query, CancellationToken ct)
{
    const string sql = "SELECT Id, PassengerName, RouteId, TotalPrice FROM Bookings WHERE Id = @Id";
    var booking = await _connection.QuerySingleOrDefaultAsync<BookingResponse>(sql, new { query.Id }, ct);

    if (booking is null)
        throw new NotFoundException($"Booking with Id {query.Id} was not found.");

    return booking;
}

// BAD — returns null silently, caller may crash
public async Task<BookingResponse?> Handle(GetBookingByIdQuery query, CancellationToken ct)
{
    const string sql = "SELECT Id, PassengerName, RouteId, TotalPrice FROM Bookings WHERE Id = @Id";
    return await _connection.QuerySingleOrDefaultAsync<BookingResponse>(sql, new { query.Id });
}
```

**Example — Angular: Component must handle loading and error states**

```typescript
// GOOD — handles loading, error, and success
@Component({
  selector: 'app-booking-detail',
  standalone: true,
  imports: [SpinnerComponent, ErrorMessageComponent, BookingCardComponent],
  template: `
    @if (loading) {
      <app-spinner />
    } @else if (error) {
      <app-error-message [message]="error" />
    } @else if (booking) {
      <app-booking-card [booking]="booking" />
    }
  `
})
export class BookingDetailComponent implements OnInit {
  booking: Booking | null = null;
  loading = true;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private bookingService: BookingService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.bookingService.getById(id).subscribe({
      next: (data) => { this.booking = data; this.loading = false; },
      error: () => { this.error = 'Failed to load booking.'; this.loading = false; }
    });
  }
}

// BAD — no loading state, no error handling, uses `any`
export class BookingDetailComponent implements OnInit {
  booking: any;

  ngOnInit(): void {
    this.bookingService.getById(this.route.snapshot.paramMap.get('id')!).subscribe(
      (data) => this.booking = data
    );
  }
}
```

---

### Idempotency and transactional consistency

**Highest severity for money-touching and irreversible side effects.** Charging a card, sending email, debiting wallets, and provisioning tenants are **not** safely retried without an idempotency strategy.

Reviewers must ask:

* What happens if this request is retried (Refit retry policy, user double-click, message redelivery)?
* If an external call succeeds but the database write fails, is money/data consistent?
* Is there a single transactional boundary, an outbox, or a documented compensating action?

**Example — .NET: Payment charge must be idempotent**

```csharp
// BAD — retry or double-submit can double-charge
public async Task<ChargeResult> Handle(ChargeCardCommand cmd, CancellationToken ct)
{
    var result = await _payments.ChargeAsync(cmd, ct);
    await _connection.ExecuteAsync(
        "INSERT INTO Payments (BookingId, ExternalId, Amount) VALUES (@BookingId, @ExternalId, @Amount)",
        new { cmd.BookingId, result.Id, cmd.Amount }, ct);
    return result;
}

// GOOD — idempotency key + persist before side effect; gateway deduplicates on key
public async Task<ChargeResult> Handle(ChargeCardCommand cmd, CancellationToken ct)
{
    var existing = await _connection.QuerySingleOrDefaultAsync<PaymentRow>(
        "SELECT ExternalId, Status FROM Payments WHERE IdempotencyKey = @Key",
        new { Key = cmd.IdempotencyKey }, ct);

    if (existing is not null)
        return new ChargeResult { Id = existing.ExternalId, Status = existing.Status };

    await _connection.ExecuteAsync("""
        INSERT INTO Payments (BookingId, IdempotencyKey, Amount, Status)
        VALUES (@BookingId, @Key, @Amount, 'pending')
        """, new { cmd.BookingId, Key = cmd.IdempotencyKey, cmd.Amount }, ct);

    var result = await _payments.ChargeAsync(
        cmd with { IdempotencyKey = cmd.IdempotencyKey }, ct);

    await _connection.ExecuteAsync(
        "UPDATE Payments SET ExternalId = @ExternalId, Status = @Status WHERE IdempotencyKey = @Key",
        new { result.Id, result.Status, Key = cmd.IdempotencyKey }, ct);

    return result;
}
```

**Example — .NET: Booking + charge must not leave inconsistent state**

```csharp
// BAD — charge succeeds, DB insert fails → customer charged, no booking record
public async Task<int> Handle(CreateBookingCommand cmd, CancellationToken ct)
{
    var charge = await _payments.ChargeAsync(new ChargeCardCommand(cmd.Amount), ct);
    return await _connection.ExecuteScalarAsync<int>(
        "INSERT INTO Bookings (...) VALUES (...); SELECT SCOPE_IDENTITY();", cmd, ct);
}

// GOOD — document the strategy explicitly (pick one per flow):
//  A) Single DB transaction for all DB writes; charge only after commit via outbox worker
//  B) Saga with compensating refund if booking insert fails after charge
//  C) Charge last, inside a handler that can refund on failure (with idempotency)

public async Task<int> Handle(CreateBookingCommand cmd, CancellationToken ct)
{
    await using var tx = _connection.BeginTransaction();
    try
    {
        var bookingId = await _connection.ExecuteScalarAsync<int>(/* insert booking */, cmd, tx, ct);
        await _outbox.EnqueueAsync(new ChargeBookingMessage(bookingId, cmd.IdempotencyKey), tx, ct);
        await tx.CommitAsync(ct);
        return bookingId;
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
}
```

**Must-fix examples:**

* Outbound payment/charge with no idempotency key.
* Retry policy on a non-idempotent POST without deduplication.
* External side effect committed before durable local state with no recovery path.

---

### Architecture and design

* Is the code placed in the correct layer?
* Does it follow Clean Architecture / CQRS boundaries?
* Is business logic kept out of API controllers?
* Are dependencies flowing in the correct direction?

**Example — .NET: Business logic must NOT live in the controller**

```csharp
// BAD — controller contains business logic
[HttpPost("bookings")]
public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
{
    if (request.PassengerCount > vehicle.Capacity)
        return BadRequest("Too many passengers.");

    var price = request.Distance * vehicle.RatePerKm;
    // ... insert into DB directly
}

// GOOD — controller delegates to CQRS handler
[HttpPost("bookings")]
public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
{
    var result = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

**Example — .NET: Outbound REST calls must use Refit, not raw `HttpClient`**

```csharp
// BAD — raw HttpClient inside a handler: untyped, no retry policy, leaks transport concerns
public class ChargeCardHandler : IRequestHandler<ChargeCardCommand, ChargeResult>
{
    private readonly HttpClient _http;
    public ChargeCardHandler(HttpClient http) => _http = http;

    public async Task<ChargeResult> Handle(ChargeCardCommand cmd, CancellationToken ct)
    {
        var res = await _http.PostAsJsonAsync("/v1/charges", cmd, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ChargeResult>(cancellationToken: ct))!;
    }
}

// GOOD — Refit interface + idempotency header; handler stays focused on behavior
public interface IPaymentGatewayApi
{
    [Post("/v1/charges")]
    Task<ChargeResult> ChargeAsync(
        [Body] ChargeCardCommand cmd,
        [Header("Idempotency-Key")] string idempotencyKey,
        CancellationToken ct);
}

public class ChargeCardHandler : IRequestHandler<ChargeCardCommand, ChargeResult>
{
    private readonly IPaymentGatewayApi _payments;
    public ChargeCardHandler(IPaymentGatewayApi payments) => _payments = payments;

    public Task<ChargeResult> Handle(ChargeCardCommand cmd, CancellationToken ct)
        => _payments.ChargeAsync(cmd, cmd.IdempotencyKey, ct);
}
```

**Example — Angular: HTTP and business logic must live in services, not components**

```typescript
// BAD — component calls HttpClient directly and filters data
export class BookingListComponent {
  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http.get('/api/bookings').subscribe((data: any) => {
      this.bookings = data.filter((b: any) => b.status === 'confirmed');
    });
  }
}

// GOOD — component delegates to a typed service
@Component({
  selector: 'app-booking-list',
  standalone: true,
  imports: [BookingCardComponent],
  template: `
    @for (booking of bookings; track booking.id) {
      <app-booking-card [booking]="booking" />
    }
  `
})
export class BookingListComponent implements OnInit {
  bookings: Booking[] = [];

  constructor(private bookingService: BookingService) {}

  ngOnInit(): void {
    this.bookingService.getConfirmedBookings().subscribe({
      next: (data) => this.bookings = data,
      error: () => this.error = 'Failed to load bookings.'
    });
  }
}
```

---

### Cancellation tokens

Every async handler, repository method, and controller action that can be cancelled must **accept and propagate** `CancellationToken` — not just list it in the signature.

* Pass `ct` to every `await` (Dapper, `HttpClient`/Refit, `Task.Delay`, file I/O).
* Do not swallow cancellation — let `OperationCanceledException` propagate unless you have a specific reason to handle it.
* Link tokens when starting child operations: `CancellationTokenSource.CreateLinkedTokenSource(ct)`.

```csharp
// BAD — ct ignored; work continues after client disconnects
public async Task<List<DriverDto>> Handle(GetDriversQuery query, CancellationToken ct)
{
    var drivers = await _connection.QueryAsync<DriverDto>(sql, param); // missing ct
    foreach (var d in drivers)
        d.Documents = await _docs.GetForDriverAsync(d.Id); // missing ct
    return drivers.ToList();
}

// GOOD — ct forwarded to every async call
public async Task<List<DriverDto>> Handle(GetDriversQuery query, CancellationToken ct)
{
    var drivers = (await _connection.QueryAsync<DriverDto>(
        new CommandDefinition(sql, param, cancellationToken: ct))).ToList();

    foreach (var d in drivers)
        ct.ThrowIfCancellationRequested();

    return drivers;
}
```

---

### Async conventions (.NET)

"Use async/await properly" means concrete rules:

| Rule | Rationale |
|------|-----------|
| **Ban** `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | Deadlock and thread-pool starvation on ASP.NET |
| **Ban** `async void` except UI event handlers | Unobserved exceptions crash the process |
| Prefer `await` over `Task.Run` for I/O | I/O is already async; extra threads waste resources |
| Name async methods `*Async` | Discoverability |
| Do not return `Task` from sync work without `async` | Use `Task.FromResult` only when truly synchronous |

```csharp
// BAD
public BookingDto GetBooking(int id) =>
    _repository.GetByIdAsync(id).Result;

// GOOD
public async Task<BookingDto> GetBookingAsync(int id, CancellationToken ct) =>
    await _repository.GetByIdAsync(id, ct);
```

---

### Code quality

* Is the code easy to read?
* Are names meaningful?
* Are methods small and focused?
* Is there unnecessary duplication?
* Is the code easy to test?

---

### Security

* Are inputs validated?
* Are SQL queries parameterized?
* Are sensitive values hidden?
* Is authorization checked properly?
* Are roles and permissions respected?

**Example — .NET: SQL must ALWAYS be parameterized**

```csharp
// BAD — SQL injection risk
var sql = $"SELECT * FROM Vehicles WHERE Id = {vehicleId}";

// GOOD — parameterized query with Dapper
const string sql = "SELECT Id, Name, Capacity, RatePerKm FROM Vehicles WHERE Id = @VehicleId";
var vehicle = await _connection.QuerySingleOrDefaultAsync<VehicleDto>(sql, new { VehicleId = vehicleId });
```

**Example — .NET: Authorization must be enforced on endpoints**

```csharp
[Authorize(Roles = "Admin,Manager")]
[HttpDelete("bookings/{id}")]
public async Task<IActionResult> Cancel(int id)
{
    await _mediator.Send(new CancelBookingCommand(id));
    return NoContent();
}
```

**Example — Angular: Auth token via interceptor, not manual headers**

```typescript
// BAD — manually attaching token in every call
this.http.get('/api/bookings', {
  headers: { Authorization: `Bearer ${localStorage.getItem('token')}` }
});

// GOOD — functional HTTP interceptor
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }

  return next(req);
};
```

**Example — Angular: XSS — never bypass sanitization without review**

```typescript
// BAD — renders unsanitized HTML; XSS if content is user-controlled
@Component({
  template: `<div [innerHTML]="userBio"></div>`
})
export class ProfileComponent {
  constructor(private sanitizer: DomSanitizer) {
    this.safeBio = sanitizer.bypassSecurityTrustHtml(userBio); // must-fix if user-controlled
  }
}

// GOOD — let Angular sanitize, or sanitize explicitly with a trusted pipeline
@Component({
  template: `<p>{{ userBio }}</p>`  // auto-escaped
})
export class ProfileComponent {}

// GOOD — if rich text is required, sanitize server-side AND use a vetted library
@Component({
  template: `<div [innerHTML]="sanitizedBio"></div>`
})
export class ProfileComponent {
  sanitizedBio = this.domSanitizer.sanitize(SecurityContext.HTML, trustedHtml) ?? '';
}
```

**Must-fix:** `bypassSecurityTrustHtml`, `bypassSecurityTrustScript`, or `bypassSecurityTrustUrl` on user-controlled or API-sourced content without documented justification.

---

### Configuration and secrets

* **Never** commit secrets, connection strings with passwords, or API keys to source control.
* **Do** store secrets in the right place for each environment:

| Environment | .NET | Angular (browser) |
|-------------|------|-------------------|
| Local dev | `dotnet user-secrets`, `appsettings.Development.json` (no secrets in git) | `environment.ts` for non-secret URLs only |
| CI/CD | Pipeline secret variables, Azure Key Vault | Build-time `environment.prod.ts` for public config only |
| Production | Azure Key Vault, App Service / container env vars | No secrets in frontend bundles — ever |

```csharp
// BAD — password in appsettings.json committed to git
"ConnectionStrings": { "Default": "Server=...;Password=SuperSecret123;" }

// GOOD — placeholder in appsettings; real value from Key Vault / env
"ConnectionStrings": { "Default": "" }
// Program.cs: builder.Configuration.AddAzureKeyVault(...)
```

---

### Performance

* Are database calls efficient?
* Is pagination used where needed?
* Are N+1 query patterns avoided?
* Is caching used appropriately (and invalidated correctly)?
* Are expensive operations avoided?
* Is async/await used properly?
* Are repeated database hits minimized?

**Example — .NET: Always paginate list endpoints**

```csharp
public async Task<PagedResult<BookingListItem>> Handle(GetBookingsQuery query, CancellationToken ct)
{
    const string countSql = "SELECT COUNT(*) FROM Bookings WHERE Status = @Status";
    const string dataSql = """
        SELECT Id, PassengerName, RouteId, TotalPrice, Status
        FROM Bookings
        WHERE Status = @Status
        ORDER BY Id
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
        """;

    var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, new { query.Status });
    var items = await _connection.QueryAsync<BookingListItem>(dataSql, new
    {
        query.Status,
        Offset = (query.Page - 1) * query.PageSize,
        query.PageSize
    });

    return new PagedResult<BookingListItem>(items.ToList(), totalCount, query.Page, query.PageSize);
}
```

**Example — .NET: Avoid N+1 queries**

```csharp
// BAD — one query per vehicle
foreach (var vehicle in vehicles)
    vehicle.LastTrip = await _connection.QuerySingleOrDefaultAsync<TripDto>(
        "SELECT TOP 1 * FROM Trips WHERE VehicleId = @Id ORDER BY StartAt DESC",
        new { vehicle.Id });

// GOOD — single query with join or batch IN clause
const string sql = """
    SELECT v.Id, v.Name, t.Id AS TripId, t.StartAt
    FROM Vehicles v
    OUTER APPLY (
        SELECT TOP 1 Id, StartAt FROM Trips WHERE VehicleId = v.Id ORDER BY StartAt DESC
    ) t
    WHERE v.TenantId = @TenantId
    """;
```

**Example — .NET: Cache with explicit invalidation**

```csharp
// BAD — caches forever; stale menu after admin change
var menu = await _cache.GetOrCreateAsync("menu", _ => LoadMenuAsync());

// GOOD — TTL + invalidate on write
var menu = await _cache.GetOrCreateAsync($"menu:{tenantId}", entry =>
{
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
    return LoadMenuAsync(tenantId, ct);
});
// On MenuUpdated event: _cache.Remove($"menu:{tenantId}");
```

**Example — Angular: trackBy in loops, unsubscribe to prevent leaks**

```typescript
// GOOD — track prevents unnecessary DOM re-renders
@for (vehicle of vehicles; track vehicle.id) {
  <app-vehicle-card [vehicle]="vehicle" />
}

// GOOD — takeUntilDestroyed prevents memory leaks
this.routeService.getById(this.id).pipe(
  takeUntilDestroyed(this.destroyRef)
).subscribe(route => this.route = route);
```

---

### Accessibility (Angular UI)

* Interactive elements are keyboard reachable (Tab, Enter, Escape).
* Form inputs have associated `<label>` or `aria-label` / `aria-labelledby`.
* Icon-only buttons have `aria-label`.
* Color is not the only indicator of state (pair with text or icon).
* Focus order matches visual order; modals trap focus and restore on close.

```html
<!-- BAD -->
<button (click)="delete()"><mat-icon>delete</mat-icon></button>

<!-- GOOD -->
<button type="button" aria-label="Delete driver" (click)="delete()">
  <mat-icon aria-hidden="true">delete</mat-icon>
</button>
```

---

### Error handling and logging

* Are exceptions handled properly?
* Are meaningful error messages returned to clients (no stack traces in production)?
* Is logging included where needed?
* Is the API response consistent?

**Logging hygiene:**

* **Never** log passwords, tokens, API keys, full credit card numbers, or national IDs.
* **Do** use structured logging (`ILogger` with named properties, not string interpolation of PII).
* **Do** include correlation IDs (`TraceIdentifier`, `Activity.Current`) in log scopes.
* Log levels: `Error` for failures needing action, `Warning` for recoverable issues, `Information` for business events, `Debug` only in dev.

```csharp
// BAD — logs PII and secrets
_logger.LogInformation("Login user {Email} password {Password}", email, password);

// GOOD — structured, no secrets
using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier }))
{
    _logger.LogInformation("User {UserId} signed in", user.Id);
}
```

**Example — .NET: Global exception handler middleware**

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
        context.Request.Method, context.Request.Path);
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new ApiErrorResponse
    {
        Message = "An unexpected error occurred.",
        StatusCode = 500
    });
}
```

**Example — Angular: Global error interceptor**

```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) router.navigate(['/login']);
      else if (error.status === 400) snackBar.open(error.error?.message || 'Invalid request.', 'Close');
      else snackBar.open('Something went wrong. Please try again.', 'Close');
      return throwError(() => error);
    })
  );
};
```

---

### Database migrations

Schema changes in this project use **idempotent SQL migrations** run at startup. Reviewers must verify:

| Check | Requirement |
|-------|-------------|
| Additive first | Prefer `IF NOT EXISTS` column/table adds over destructive DDL |
| Backward compatible | Old app version + new schema, and new app + old schema (during rolling deploy) |
| No data loss | No `DROP COLUMN` / `DROP TABLE` without explicit migration plan and backup |
| Rollback | Document manual rollback steps if auto-rollback is not possible |
| Indexes | Add indexes for new filter/join columns on large tables |
| Tenant scope | New operational tables include `TenantId` where applicable |

**Must-fix:** Destructive migration with no rollout plan, or migration that breaks running instances mid-deploy.

---

### API contracts and versioning

* Do not rename or remove JSON properties clients depend on without a migration path.
* Prefer **additive** changes (new optional fields) over breaking renames.
* Document breaking changes in the PR and notify frontend/mobile owners.
* Use consistent envelope shape (`ApiResponse<T>`) — do not mix raw DTOs and wrapped responses on the same resource.
* For external Refit clients, treat the remote OpenAPI/spec as a contract — contract tests catch drift mocks miss.

**Must-fix:** Removing or renaming a field the Angular app reads without a coordinated frontend change in the same release.

---

### Dependencies

When a PR adds NuGet or npm packages:

* Is the package actively maintained and appropriate for the task?
* Run vulnerability scan (`dotnet list package --vulnerable`, `npm audit`).
* Check license compatibility for commercial use.
* Avoid duplicate libraries (two HTTP clients, two date libraries, two chart libraries).
* Pin major versions consciously — document why if adding a heavy dependency.

---

### Testing

Testing is not unit-tests-only. Match test type to risk.

| Type | When | Tooling |
|------|------|---------|
| **Unit** | Handlers, validators, pure logic | xUnit, Jasmine/Karma |
| **Integration** | SQL, migrations, API endpoints with test DB | WebApplicationFactory, Testcontainers |
| **Contract** | Refit clients against real or recorded API | WireMock, Pact, or recorded HTTP fixtures |
| **E2E** | Critical user journeys (login, booking, payment) | Playwright / Cypress (when introduced) |

**Quality rules:**

* Tests must be **deterministic** — no flaky time/network dependencies without mocking.
* Each test asserts one behavior; name tests `Method_Scenario_Expected`.
* Mocking `IPaymentGatewayApi` verifies *your* code; a **contract test** verifies the gateway still matches the interface the provider exposes.
* New handlers and non-trivial bug fixes should include tests; money and auth flows **require** tests.

**Example — .NET: xUnit test for a CQRS handler**

```csharp
[Fact]
public async Task Handle_ValidCommand_ReturnsBookingId()
{
    var command = new CreateBookingCommand { PassengerName = "Ali", RouteId = 1 };
    _connectionMock.SetupDapperAsync(/* ... */).ReturnsAsync(42);

    var result = await _handler.Handle(command, CancellationToken.None);

    Assert.Equal(42, result.Id);
}
```

**Example — .NET: Idempotent charge — retry must not double-call gateway**

```csharp
[Fact]
public async Task Handle_DuplicateIdempotencyKey_ReturnsExistingWithoutCallingGateway()
{
    _connectionMock.Setup(/* existing payment row */);
    var cmd = new ChargeCardCommand { IdempotencyKey = "key-1", Amount = 100 };

    await _handler.Handle(cmd, CancellationToken.None);

    _payments.Verify(p => p.ChargeAsync(It.IsAny<ChargeCardCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

**Example — Angular: Jasmine test for a service**

```typescript
it('should fetch confirmed bookings', () => {
  service.getConfirmedBookings().subscribe(bookings => {
    expect(bookings.length).toBe(1);
  });
  const req = httpMock.expectOne(r => r.url === '/api/bookings');
  req.flush([{ id: 1, passengerName: 'Ali', status: 'confirmed' }]);
});
```

---

## Code review rules

* Review the code, not the person.
* Be specific in comments.
* Suggest improvement, not just criticism.
* Do not approve code you do not understand.
* **Do not merge with unresolved must-fix items.**
* Keep review comments focused on the current change only.
* Prefer asking questions ("What happens if charge succeeds but insert fails?") over accusations.

---

## Review comment types

### Must-fix

Use when the change can break functionality, architecture, security, data integrity, or cause **double charges / inconsistent money state**.

**Blocks merge** until fixed or explicitly withdrawn.

**Examples:**

* "This SQL uses string concatenation — must use parameterized queries with Dapper."
* "Business logic in the controller — move to the CQRS handler."
* "This endpoint has no `[Authorize]` attribute — anyone can access it."
* "Payment charge has no idempotency key — retry can double-charge."
* "Charge succeeds before booking is persisted — no compensating action documented."
* "Raw `HttpClient` used for an outbound REST call — define a Refit interface instead."
* "The Angular component calls `HttpClient` directly — use a service."
* "`bypassSecurityTrustHtml` on API-sourced content — XSS risk."

### Should-fix

Use when the change is correct but weak in quality, performance, or maintainability.

**Does not block merge** — author should fix or reply with rationale.

**Examples:**

* "This query does `SELECT *` — select only the fields used in the DTO."
* "N+1 loop over drivers — batch or join."
* "Add `takeUntilDestroyed` to prevent subscription leaks."
* "`CancellationToken` not passed to Dapper call."
* "The handler method is 80 lines — split into smaller private methods."

### Nice-to-have

Use for style improvements, naming, formatting, or optional refactoring.

**Never blocks merge.**

**Examples:**

* "Consider renaming `data` to `bookingList` for clarity."
* "This could use `async` pipe instead of manual subscription."

---

## Author checklist (before requesting review)

* [ ] Linked ticket / requirement; PR description explains what and why
* [ ] Self-reviewed the diff
* [ ] CI green locally (build, lint, tests)
* [ ] PR is focused; unrelated changes split out
* [ ] UI screenshots attached if applicable
* [ ] No secrets, tokens, or PII in logs or committed config
* [ ] Migrations are additive and idempotent
* [ ] Money/external side effects have idempotency or documented consistency strategy
* [ ] Tests added or updated for changed behavior

---

## Reviewer checklist

Before approving, confirm:

* Business logic is correct
* Validation is complete (FluentValidation on .NET, reactive form validators on Angular)
* **Idempotency and transactional boundaries** for payments and irreversible side effects
* No hardcoded secrets; secrets loaded from Key Vault / env / user-secrets
* No SQL injection risk (all Dapper queries parameterized)
* No broken dependency between layers (controller → handler → repository)
* `CancellationToken` propagated to all async I/O
* No `.Result` / `.Wait()` / `async void` (except UI events)
* Outbound REST via Refit with timeouts; retry only on idempotent operations
* No N+1 queries on list endpoints
* Angular: no unsafe `innerHTML` / `bypassSecurityTrust*` on untrusted content
* Accessibility basics on new UI (labels, keyboard, focus)
* Logging: structured, no PII/secrets; errors logged with correlation context
* Migrations safe for rolling deploy
* API changes backward compatible or coordinated with clients
* New dependencies justified and vulnerability-scanned
* Tests added or updated (unit minimum; integration for SQL/API; contract for external APIs)
* Naming is consistent (`PascalCase` for .NET, `camelCase` for TypeScript)
* Code follows project standards (Clean Architecture, CQRS, typed models)
* **No unresolved must-fix comments**
