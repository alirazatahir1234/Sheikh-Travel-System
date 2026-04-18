# PR Review Guidelines

## Goal

PR review is the final quality gate before merging code into the main branch.

## Reviewer responsibilities

The reviewer must confirm:

* requirements are satisfied
* architecture is respected
* code is secure
* tests are adequate
* changes are maintainable

---

## PR review checklist

### Functional checks

* Does the code solve the task?
* Are all acceptance criteria met?
* Are edge cases handled?

**Example — .NET: Verify the handler covers edge cases**

```csharp
// Reviewer should check: what happens if the vehicle is inactive?
public async Task<BookingCreatedResponse> Handle(CreateBookingCommand command, CancellationToken ct)
{
    const string vehicleSql = "SELECT Id, Capacity, IsActive FROM Vehicles WHERE Id = @VehicleId";
    var vehicle = await _connection.QuerySingleOrDefaultAsync<VehicleDto>(vehicleSql, new { command.VehicleId });

    if (vehicle is null)
        throw new NotFoundException($"Vehicle {command.VehicleId} not found.");

    if (!vehicle.IsActive)
        throw new BusinessRuleException($"Vehicle {command.VehicleId} is not active.");

    if (command.PassengerCount > vehicle.Capacity)
        throw new BusinessRuleException($"Vehicle capacity is {vehicle.Capacity}, but {command.PassengerCount} passengers requested.");

    // ... proceed with booking creation
}
```

**Example — Angular: Verify the component disables submit during loading**

```typescript
// Reviewer should check: is double-submit prevented?
<button mat-raised-button type="submit" [disabled]="form.invalid || loading">
  @if (loading) { Creating... } @else { Create Booking }
</button>
```

---

### Technical checks

* Is the code in the right layer?
* Is the naming clean?
* Is the code modular?
* Is duplication minimized?
* Are dependencies correct?

**Example — .NET: Flag business logic in wrong layer**

```csharp
// REVIEW FLAG — this price calculation belongs in the handler, not the controller
[HttpPost("bookings")]
public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
{
    var price = request.Distance * request.RatePerKm; // ❌ Business logic in controller
    // ...
}

// CORRECT — controller only delegates
[HttpPost("bookings")]
public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
{
    var result = await _mediator.Send(command); // ✅ Handler does the work
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

**Example — Angular: Flag direct HTTP calls in components**

```typescript
// REVIEW FLAG — component should not call HttpClient directly
export class VehicleListComponent {
  constructor(private http: HttpClient) {} // ❌ Wrong dependency

  ngOnInit(): void {
    this.http.get('/api/vehicles').subscribe(...); // ❌ Should use VehicleService
  }
}

// CORRECT — component uses a service
export class VehicleListComponent {
  constructor(private vehicleService: VehicleService) {} // ✅

  ngOnInit(): void {
    this.vehicleService.getAll().subscribe(...); // ✅
  }
}
```

---

### Security checks

* Are inputs validated?
* Is authorization enforced?
* Are secrets protected?
* Is SQL safe?
* Is logging safe?

**Example — .NET: Flag missing authorization**

```csharp
// REVIEW FLAG — no [Authorize], any anonymous user can delete bookings
[HttpDelete("bookings/{id}")]
public async Task<IActionResult> Cancel(int id) // ❌ Missing [Authorize]
{
    await _mediator.Send(new CancelBookingCommand(id));
    return NoContent();
}

// CORRECT
[Authorize(Roles = "Admin,Manager")]
[HttpDelete("bookings/{id}")]
public async Task<IActionResult> Cancel(int id) // ✅
{
    await _mediator.Send(new CancelBookingCommand(id));
    return NoContent();
}
```

**Example — .NET: Flag unsafe SQL**

```csharp
// REVIEW FLAG — SQL injection vulnerability
var sql = $"SELECT * FROM Bookings WHERE PassengerName = '{name}'"; // ❌

// CORRECT
const string sql = "SELECT Id, PassengerName FROM Bookings WHERE PassengerName = @Name"; // ✅
var result = await _connection.QueryAsync<BookingDto>(sql, new { Name = name });
```

**Example — Angular: Flag hardcoded secrets**

```typescript
// REVIEW FLAG — API key exposed in source code
const API_KEY = 'sk-abc123xyz789'; // ❌ Never commit secrets

// CORRECT — use environment config (non-secret values only)
const apiUrl = environment.apiUrl; // ✅
```

**Example — Angular: Flag missing route guard**

```typescript
// REVIEW FLAG — admin page has no guard
{ path: 'admin/dashboard', component: AdminDashboardComponent } // ❌

// CORRECT
{ path: 'admin/dashboard', component: AdminDashboardComponent, canActivate: [authGuard, adminGuard] } // ✅
```

---

### Quality checks

* Are tests present?
* Are errors handled properly?
* Is the code readable?
* Is the PR small enough and well-scoped?

**Example — .NET: Verify tests cover both success and failure**

```csharp
// Reviewer should confirm BOTH of these exist:

[Fact]
public async Task Handle_ValidCommand_CreatesBooking()
{
    // ... success path test
}

[Fact]
public async Task Handle_InvalidVehicle_ThrowsNotFoundException()
{
    // ... failure path test
}

[Fact]
public async Task Handle_ExceedsCapacity_ThrowsBusinessRuleException()
{
    // ... business rule violation test
}
```

**Example — Angular: Verify tests cover form validation and submit**

```typescript
// Reviewer should confirm these exist:

it('should be invalid when passenger name is empty', () => { ... });
it('should be valid when all fields filled correctly', () => { ... });
it('should call service on valid submit', () => { ... });
it('should NOT call service on invalid submit', () => { ... });
it('should show error message on API failure', () => { ... });
```

---

## Review decision rules

### Approve

Use when:

* the code is correct and handles edge cases
* tests cover success and failure paths
* architecture boundaries are respected
* no security issues remain

### Request changes

Use when:

* business logic is in the controller instead of the handler
* SQL is not parameterized
* authorization is missing on protected endpoints
* tests are missing for critical business rules
* `any` type is used instead of proper interfaces in Angular
* component calls `HttpClient` directly instead of using a service

### Comment only

Use when:

* the code is correct and safe
* but there are minor suggestions: naming improvements, optional refactoring, style preferences
* example: "Consider using `async` pipe instead of manual subscribe" or "This method name could be more descriptive"
