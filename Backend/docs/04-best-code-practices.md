# Best Code Practices

## General principles

* Follow SOLID principles.
* Write code that is easy to read, not just code that works.
* Keep methods short and focused.
* Prefer clarity over cleverness.
* Avoid duplication.
* Make intent obvious through naming.
* Keep public contracts stable and versioned when needed.
* Favor composition over inheritance unless inheritance is clearly justified.
* Make behavior deterministic (avoid hidden side effects).
* Fail fast on invalid state; fail safely on external dependency issues.

---

## Additional high-value practices

### Security and secrets

* Never log secrets, tokens, passwords, or PII.
* Keep secrets in environment variables / secure vaults, never in source code.
* Validate and sanitize all external inputs (query, body, headers, route params).
* Apply least-privilege access for DB users, service accounts, and API permissions.
* Use HTTPS everywhere and enforce secure headers at the API boundary.

### Async, cancellation, and resilience (.NET)

* Pass `CancellationToken` through handlers, repositories, and external calls.
* Use timeouts and retry policies for transient failures (e.g., HTTP/database connectivity).
* Do not block async flows (`.Result`, `.Wait()`); use `await` end-to-end.
* Distinguish transient vs permanent errors and return appropriate status codes.

### External HTTP calls (.NET) — use Refit

For any outbound REST API call (payment gateway, SMS/email provider, mapping/geocoding service, partner APIs, etc.) declare a **typed Refit interface** and let Refit generate the client. Raw `HttpClient` / `IHttpClientFactory` should not appear inside handlers or services.

**Why Refit over raw `HttpClient`:**

* The wire contract lives in an interface — callers are strongly typed end-to-end.
* Request building (routes, query strings, headers, body, auth) is declarative.
* Responses deserialize automatically; errors surface as `ApiException` with the response body intact.
* Trivial to mock in tests — just implement the interface, no `HttpMessageHandler` gymnastics.
* Integrates cleanly with `IHttpClientFactory`, Polly (timeouts/retries/circuit-breaker), and DI.

```csharp
// 1. Declare the external API as an interface (Infrastructure layer)
public interface ISmsGatewayApi
{
    [Post("/v1/messages")]
    Task<SendSmsResponse> SendAsync([Body] SendSmsRequest request, CancellationToken ct);

    [Get("/v1/messages/{id}")]
    Task<SmsStatusResponse> GetStatusAsync(string id, CancellationToken ct);
}

public sealed record SendSmsRequest(string To, string Body);
public sealed record SendSmsResponse(string Id, string Status);
public sealed record SmsStatusResponse(string Id, string Status, DateTime SentAt);

// 2. Register once in Program.cs / DependencyInjection.cs
builder.Services
    .AddRefitClient<ISmsGatewayApi>()
    .ConfigureHttpClient((sp, c) =>
    {
        var opts = sp.GetRequiredService<IOptions<SmsGatewayOptions>>().Value;
        c.BaseAddress = new Uri(opts.BaseUrl);
        c.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
        c.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddTransientHttpErrorPolicy(p =>
        p.WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))));

// 3. Inject the interface — NOT HttpClient — into your handler
public sealed class NotifyBookingConfirmedHandler(ISmsGatewayApi sms, ILogger<NotifyBookingConfirmedHandler> logger)
    : INotificationHandler<BookingConfirmedEvent>
{
    public async Task Handle(BookingConfirmedEvent notification, CancellationToken ct)
    {
        try
        {
            var res = await sms.SendAsync(
                new SendSmsRequest(notification.PhoneNumber, $"Your booking #{notification.BookingId} is confirmed."),
                ct);

            logger.LogInformation("SMS {SmsId} queued for booking {BookingId}", res.Id, notification.BookingId);
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            logger.LogWarning(ex, "SMS gateway rejected payload for booking {BookingId}", notification.BookingId);
            throw new BusinessRuleException("Unable to send booking SMS.");
        }
    }
}
```

**Bad:**

```csharp
// BAD — raw HttpClient, string-concatenated URL, manual JSON, swallowed failure mode
public class NotifyBookingConfirmedHandler
{
    private readonly HttpClient _http;
    public NotifyBookingConfirmedHandler(HttpClient http) => _http = http;

    public async Task SendAsync(string phone, string message)
    {
        var payload = new StringContent($"{{\"to\":\"{phone}\",\"body\":\"{message}\"}}", Encoding.UTF8, "application/json");
        var res = await _http.PostAsync("/v1/messages", payload); // ❌ no CancellationToken, no typed result, no retry
        res.EnsureSuccessStatusCode();                             // ❌ loses response body on failure
    }
}
```

**Rules:**

* Every external REST dependency gets its own Refit interface in `Infrastructure/ExternalClients/` (or a dedicated integration project).
* Register with `AddRefitClient<T>()` + `ConfigureHttpClient` for base URL, auth, and timeout.
* Attach Polly policies for retries, circuit-breakers, and timeouts — do not hand-roll them.
* Put secrets in configuration/secret store; bind with strongly typed options (`IOptions<T>`).
* Always accept and forward `CancellationToken` in the interface signatures.
* Handle `Refit.ApiException` explicitly for known non-success paths; let truly unexpected errors bubble to the global handler.
* Mock in tests by implementing the interface (or using `Moq.Mock<ISmsGatewayApi>`) — never spin up `HttpMessageHandler` mocks.

> Scope note — this guidance is for **.NET outbound HTTP calls**. The Angular frontend continues to use Angular's `HttpClient` through typed services + interceptors (see the "Typed API service pattern" section below); Refit does not exist in the Angular/TypeScript ecosystem.

### Performance and scalability

* Measure first (profiling/metrics), optimize second.
* Avoid N+1 query patterns; batch and project only required fields.
* Paginate any endpoint that can grow unbounded.
* Cache expensive, frequently read data with clear invalidation strategy.

### Configuration and environment management

* Keep environment-specific settings out of code.
* Use strongly typed config objects with startup validation.
* Provide safe defaults and explicit overrides for production.
* Document required environment variables in project docs.

### Pull request and review checklist

* Is the change small, focused, and reversible?
* Are edge cases and failure paths covered by tests?
* Is the API contract backward-compatible?
* Are logs useful and free of sensitive data?
* Are migrations/config changes included when needed?

---

## Naming

### .NET naming conventions

Use `PascalCase` for classes, methods, properties, and public members.
Use `camelCase` with underscore prefix for private fields.

```csharp
// GOOD — clear, descriptive names
public class CreateBookingCommand
{
    public string PassengerName { get; set; } = string.Empty;
    public int RouteId { get; set; }
    public int VehicleId { get; set; }
    public int PassengerCount { get; set; }
}

public class CreateBookingHandler : IRequestHandler<CreateBookingCommand, BookingCreatedResponse>
{
    private readonly IDbConnection _connection;

    public CreateBookingHandler(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<BookingCreatedResponse> Handle(CreateBookingCommand command, CancellationToken ct)
    {
        // ...
    }
}

// BAD — vague, inconsistent names
public class BookingCmd
{
    public string name { get; set; }
    public int rid { get; set; }
    public int data1 { get; set; }
}
```

**Standard .NET naming patterns:**

| Type | Pattern | Example |
|------|---------|---------|
| Command | `Create{Entity}Command` | `CreateBookingCommand` |
| Query | `Get{Entity}By{Field}Query` | `GetVehicleByIdQuery` |
| Handler | `{CommandOrQuery}Handler` | `CreateBookingHandler` |
| Validator | `{Command}Validator` | `CreateBookingCommandValidator` |
| DTO (Request) | `Create{Entity}Request` | `CreateBookingRequest` |
| DTO (Response) | `{Entity}Response` | `BookingResponse` |
| Repository | `I{Entity}Repository` | `IBookingRepository` |
| Controller | `{Entity}sController` | `BookingsController` |

### Angular naming conventions

Use `camelCase` for variables, methods, and properties.
Use `PascalCase` for classes, interfaces, and types.
Use `kebab-case` for file names.

```typescript
// GOOD — clear, typed, consistent
// booking.model.ts
export interface Booking {
  id: number;
  passengerName: string;
  routeId: number;
  totalPrice: number;
  status: BookingStatus;
}

export type BookingStatus = 'pending' | 'confirmed' | 'cancelled';

// create-booking-request.model.ts
export interface CreateBookingRequest {
  passengerName: string;
  routeId: number;
  vehicleId: number;
  passengerCount: number;
}

// BAD — uses `any`, no interface, vague names
let data: any;
let obj: any;
let temp = {};
```

**Standard Angular naming patterns:**

| Type | Pattern | Example |
|------|---------|---------|
| Component | `{feature}.component.ts` | `booking-list.component.ts` |
| Service | `{entity}.service.ts` | `booking.service.ts` |
| Model | `{entity}.model.ts` | `booking.model.ts` |
| Guard | `{name}.guard.ts` | `auth.guard.ts` |
| Interceptor | `{name}.interceptor.ts` | `auth.interceptor.ts` |
| Pipe | `{name}.pipe.ts` | `currency-format.pipe.ts` |
| Module (lazy) | `{feature}.routes.ts` | `booking.routes.ts` |

---

## Layer separation

### .NET — Clean Architecture layers

```
src/
├── SheikhTravel.Api/              # Controllers, middleware, Program.cs
├── SheikhTravel.Application/      # Commands, Queries, Handlers, Validators, DTOs
├── SheikhTravel.Domain/           # Entities, Enums, Exceptions, Interfaces
└── SheikhTravel.Infrastructure/   # Dapper repositories, DB connection, external services
```

**Rules:**

* **Api** → only handles HTTP request/response. Delegates to `_mediator.Send()`.
* **Application** → contains CQRS commands, queries, handlers, validators, DTOs.
* **Domain** → contains entities, value objects, enums, domain exceptions. No dependencies.
* **Infrastructure** → contains Dapper SQL, DB connections, external API clients.

```csharp
// Api layer — thin controller
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BookingsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetBookingByIdQuery(id));
        return Ok(result);
    }
}

// Application layer — handler with business logic
public class CreateBookingHandler : IRequestHandler<CreateBookingCommand, BookingCreatedResponse>
{
    private readonly IDbConnection _connection;
    private readonly ILogger<CreateBookingHandler> _logger;

    public CreateBookingHandler(IDbConnection connection, ILogger<CreateBookingHandler> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<BookingCreatedResponse> Handle(CreateBookingCommand command, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO Bookings (PassengerName, RouteId, VehicleId, PassengerCount, Status, CreatedAt)
            VALUES (@PassengerName, @RouteId, @VehicleId, @PassengerCount, 'Pending', GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        var id = await _connection.ExecuteScalarAsync<int>(sql, command);
        _logger.LogInformation("Booking {BookingId} created for passenger {PassengerName}", id, command.PassengerName);

        return new BookingCreatedResponse { Id = id };
    }
}
```

### Angular — Feature-based structure

```
src/app/
├── core/                          # Singleton services, interceptors, guards
│   ├── interceptors/
│   │   ├── auth.interceptor.ts
│   │   └── error.interceptor.ts
│   ├── guards/
│   │   └── auth.guard.ts
│   └── services/
│       └── auth.service.ts
├── shared/                        # Reusable components, pipes, directives
│   ├── components/
│   │   ├── spinner/
│   │   └── error-message/
│   └── models/
│       └── api-response.model.ts
├── features/                      # Feature modules
│   ├── bookings/
│   │   ├── components/
│   │   │   ├── booking-list/
│   │   │   │   ├── booking-list.component.ts
│   │   │   │   ├── booking-list.component.html
│   │   │   │   └── booking-list.component.spec.ts
│   │   │   └── booking-form/
│   │   ├── models/
│   │   │   └── booking.model.ts
│   │   ├── services/
│   │   │   └── booking.service.ts
│   │   └── booking.routes.ts
│   ├── vehicles/
│   └── routes/
├── app.component.ts
├── app.config.ts
└── app.routes.ts
```

**Rules:**

* **Components** → only handle template rendering and user interaction.
* **Services** → handle HTTP calls, data transformation, business logic.
* **Models** → define TypeScript interfaces for all data shapes.
* **Guards/Interceptors** → handle cross-cutting concerns (auth, errors).

---

## Validation

### .NET — FluentValidation

```csharp
public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.PassengerName)
            .NotEmpty().WithMessage("Passenger name is required.")
            .MaximumLength(100).WithMessage("Passenger name must not exceed 100 characters.");

        RuleFor(x => x.RouteId)
            .GreaterThan(0).WithMessage("A valid route must be selected.");

        RuleFor(x => x.VehicleId)
            .GreaterThan(0).WithMessage("A valid vehicle must be selected.");

        RuleFor(x => x.PassengerCount)
            .InclusiveBetween(1, 50).WithMessage("Passenger count must be between 1 and 50.");
    }
}

// Register in DI (Program.cs)
builder.Services.AddValidatorsFromAssemblyContaining<CreateBookingCommandValidator>();
```

### Angular — Reactive Forms validation

```typescript
@Component({
  selector: 'app-booking-form',
  standalone: true,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <mat-form-field>
        <mat-label>Passenger Name</mat-label>
        <input matInput formControlName="passengerName" />
        @if (form.controls.passengerName.hasError('required')) {
          <mat-error>Passenger name is required.</mat-error>
        }
        @if (form.controls.passengerName.hasError('maxlength')) {
          <mat-error>Name must not exceed 100 characters.</mat-error>
        }
      </mat-form-field>

      <mat-form-field>
        <mat-label>Passenger Count</mat-label>
        <input matInput type="number" formControlName="passengerCount" />
        @if (form.controls.passengerCount.hasError('min')) {
          <mat-error>At least 1 passenger required.</mat-error>
        }
        @if (form.controls.passengerCount.hasError('max')) {
          <mat-error>Maximum 50 passengers allowed.</mat-error>
        }
      </mat-form-field>

      <button mat-raised-button color="primary" type="submit" [disabled]="form.invalid">
        Create Booking
      </button>
    </form>
  `
})
export class BookingFormComponent {
  private bookingService = inject(BookingService);
  private router = inject(Router);

  form = new FormGroup({
    passengerName: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    routeId: new FormControl<number | null>(null, [Validators.required]),
    vehicleId: new FormControl<number | null>(null, [Validators.required]),
    passengerCount: new FormControl(1, [Validators.required, Validators.min(1), Validators.max(50)])
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    const request: CreateBookingRequest = {
      passengerName: this.form.value.passengerName!,
      routeId: this.form.value.routeId!,
      vehicleId: this.form.value.vehicleId!,
      passengerCount: this.form.value.passengerCount!
    };

    this.bookingService.create(request).subscribe({
      next: (res) => this.router.navigate(['/bookings', res.id]),
      error: () => { /* handled by error interceptor */ }
    });
  }
}
```

---

## Database practices

### .NET — Dapper patterns

```csharp
// GOOD — parameterized, readable, returns only needed columns
public async Task<VehicleDto?> GetByIdAsync(int vehicleId)
{
    const string sql = """
        SELECT Id, Name, Type, Capacity, RatePerKm, FuelAverage, IsActive
        FROM Vehicles
        WHERE Id = @VehicleId AND IsActive = 1
        """;

    return await _connection.QuerySingleOrDefaultAsync<VehicleDto>(sql, new { VehicleId = vehicleId });
}

// GOOD — paginated list query
public async Task<PagedResult<BookingListItem>> GetBookingsAsync(int page, int pageSize, string? status)
{
    const string countSql = "SELECT COUNT(*) FROM Bookings WHERE (@Status IS NULL OR Status = @Status)";
    const string dataSql = """
        SELECT Id, PassengerName, RouteId, TotalPrice, Status, CreatedAt
        FROM Bookings
        WHERE (@Status IS NULL OR Status = @Status)
        ORDER BY CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
        """;

    var parameters = new { Status = status, Offset = (page - 1) * pageSize, PageSize = pageSize };

    var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
    var items = (await _connection.QueryAsync<BookingListItem>(dataSql, parameters)).ToList();

    return new PagedResult<BookingListItem>(items, totalCount, page, pageSize);
}

// BAD — SQL injection, SELECT *, no pagination
var sql = $"SELECT * FROM Bookings WHERE Status = '{status}'";
var bookings = await _connection.QueryAsync(sql);
```

---

## API practices

### .NET — Consistent response structure

**Controller action style rule (Commands + Queries):**

* Always assign mediator output to `var result` before returning.
* Use block-bodied actions for readability, logging hooks, and future extension.
* Avoid expression-bodied controller actions such as `=> Ok(await Mediator.Send(query));`.

```csharp
// Standard API response wrapper (generic)
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? TraceId { get; init; }
    public ApiError? Error { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Request completed successfully.", string? traceId = null)
        => new() { Success = true, Data = data, Message = message, TraceId = traceId };

    public static ApiResponse<T> Fail(string code, string message, int statusCode, IEnumerable<string>? details = null, string? traceId = null)
        => new()
        {
            Success = false,
            Message = message,
            TraceId = traceId,
            Error = new ApiError
            {
                Code = code,
                Message = message,
                StatusCode = statusCode,
                Details = details?.ToList() ?? []
            }
        };
}

public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public List<string> Details { get; init; } = [];
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Controller returns consistent responses
[HttpGet]
public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
{
    var result = await _mediator.Send(new GetBookingsQuery(page, pageSize));
    return Ok(ApiResponse<PagedResult<BookingListItem>>.Ok(
        result,
        traceId: HttpContext.TraceIdentifier
    ));
}

[HttpGet("{id}")]
public async Task<IActionResult> GetById(int id)
{
    var result = await _mediator.Send(new GetBookingByIdQuery(id));
    return Ok(ApiResponse<BookingResponse>.Ok(
        result,
        traceId: HttpContext.TraceIdentifier
    ));
}

// Preferred style for all endpoints (including query events)
[HttpGet]
public async Task<IActionResult> GetAll([FromQuery] GetFuelLogsQuery query)
{
    var result = await Mediator.Send(query);
    return Ok(result);
}

[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateFuelLogCommand command)
{
    var result = await Mediator.Send(command);
    return Created(string.Empty, result);
}
```

### Angular — Typed API service pattern

```typescript
// api-response.model.ts
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string;
  traceId?: string;
  error?: ApiError;
}

export interface ApiError {
  code: string;
  message: string;
  statusCode: number;
  details: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// booking.service.ts
@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly apiUrl = `${environment.apiUrl}/bookings`;
  private http = inject(HttpClient);

  getAll(page = 1, pageSize = 20): Observable<PagedResult<Booking>> {
    return this.http.get<ApiResponse<PagedResult<Booking>>>(this.apiUrl, {
      params: { page: page.toString(), pageSize: pageSize.toString() }
    }).pipe(
      map(res => {
        if (!res.success || !res.data) {
          throw new Error(res.error?.message ?? res.message);
        }
        return res.data;
      })
    );
  }

  getById(id: number): Observable<Booking> {
    return this.http.get<ApiResponse<Booking>>(`${this.apiUrl}/${id}`).pipe(
      map(res => {
        if (!res.success || !res.data) {
          throw new Error(res.error?.message ?? res.message);
        }
        return res.data;
      })
    );
  }

  create(request: CreateBookingRequest): Observable<BookingCreatedResponse> {
    return this.http.post<ApiResponse<BookingCreatedResponse>>(this.apiUrl, request).pipe(
      map(res => {
        if (!res.success || !res.data) {
          throw new Error(res.error?.message ?? res.message);
        }
        return res.data;
      })
    );
  }
}
```

### Generic API response model to use across the whole application

Use one shared response contract for all endpoints (success and error) so frontend, backend, logging, and monitoring stay consistent.

```csharp
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? TraceId { get; init; }
    public ApiError? Error { get; init; }
}

public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;   // e.g. "VALIDATION_ERROR"
    public string Message { get; init; } = string.Empty;
    public int StatusCode { get; init; }                // e.g. 400, 404, 500
    public List<string> Details { get; init; } = [];
}
```

```typescript
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string;
  traceId?: string;
  error?: ApiError;
}

export interface ApiError {
  code: string;
  message: string;
  statusCode: number;
  details: string[];
}
```

**Recommended rules:**

* Always return `ApiResponse<T>` from API endpoints.
* Set `traceId` from the request context for debugging and support.
* Use stable error `code` values (do not depend on raw exception text).
* For validation errors, return `code = "VALIDATION_ERROR"` with field-level details.
* Keep `message` user-safe; keep technical details in logs.

---

## Error handling

### .NET — Custom exceptions and global handler

```csharp
// Domain exceptions
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

// Usage in handler
public async Task<BookingResponse> Handle(GetBookingByIdQuery query, CancellationToken ct)
{
    const string sql = "SELECT Id, PassengerName, TotalPrice, Status FROM Bookings WHERE Id = @Id";
    var booking = await _connection.QuerySingleOrDefaultAsync<BookingResponse>(sql, new { query.Id });

    if (booking is null)
        throw new NotFoundException($"Booking with Id {query.Id} was not found.");

    return booking;
}
```

### Angular — Error handling in components and services

```typescript
// Component-level error handling
onSubmit(): void {
  if (this.form.invalid) return;

  this.loading = true;
  this.bookingService.create(this.form.value as CreateBookingRequest).subscribe({
    next: (res) => {
      this.loading = false;
      this.router.navigate(['/bookings', res.id]);
    },
    error: (err: HttpErrorResponse) => {
      this.loading = false;
      if (err.status === 400 && err.error?.errors) {
        this.validationErrors = err.error.errors;
      }
      // Global errors (500, 401) handled by errorInterceptor
    }
  });
}
```

---

## Logging

### .NET — Structured logging with Serilog/ILogger

```csharp
// GOOD — structured, contextual logging
_logger.LogInformation("Creating booking for passenger {PassengerName} on route {RouteId}", command.PassengerName, command.RouteId);
_logger.LogWarning("Vehicle {VehicleId} capacity exceeded: requested {Requested}, available {Available}", vehicleId, requested, available);
_logger.LogError(ex, "Failed to create booking for passenger {PassengerName}", command.PassengerName);

// BAD — unstructured, exposes sensitive data
_logger.LogInformation($"Creating booking: {JsonSerializer.Serialize(command)}");  // may log sensitive data
Console.WriteLine("Booking created");  // not logged to infrastructure
```

---

## Testing

### .NET — xUnit with Moq

```csharp
public class CalculateBookingPriceHandlerTests
{
    private readonly Mock<IDbConnection> _connectionMock = new();
    private readonly CalculateBookingPriceHandler _handler;

    public CalculateBookingPriceHandlerTests()
    {
        _handler = new CalculateBookingPriceHandler(_connectionMock.Object);
    }

    [Theory]
    [InlineData(100, 15.5, 1550)]   // 100 km × 15.5 rate
    [InlineData(50, 20.0, 1000)]    // 50 km × 20 rate
    [InlineData(0, 15.5, 0)]        // 0 km edge case
    public async Task Handle_CalculatesCorrectPrice(decimal distance, decimal ratePerKm, decimal expectedPrice)
    {
        var query = new CalculateBookingPriceQuery { RouteId = 1, VehicleId = 1 };

        _connectionMock.SetupDapperAsync(c =>
            c.QuerySingleOrDefaultAsync<RouteDto>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(new RouteDto { Distance = distance });

        _connectionMock.SetupDapperAsync(c =>
            c.QuerySingleOrDefaultAsync<VehicleDto>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(new VehicleDto { RatePerKm = ratePerKm });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(expectedPrice, result.TotalPrice);
    }

    [Fact]
    public async Task Handle_InvalidRoute_ThrowsNotFoundException()
    {
        var query = new CalculateBookingPriceQuery { RouteId = 999, VehicleId = 1 };

        _connectionMock.SetupDapperAsync(c =>
            c.QuerySingleOrDefaultAsync<RouteDto>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync((RouteDto?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }
}
```

### Angular — Jasmine with TestBed

```typescript
describe('BookingFormComponent', () => {
  let component: BookingFormComponent;
  let fixture: ComponentFixture<BookingFormComponent>;
  let bookingService: jasmine.SpyObj<BookingService>;

  beforeEach(async () => {
    bookingService = jasmine.createSpyObj('BookingService', ['create']);

    await TestBed.configureTestingModule({
      imports: [BookingFormComponent, NoopAnimationsModule],
      providers: [
        { provide: BookingService, useValue: bookingService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BookingFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should mark form invalid when passenger name is empty', () => {
    component.form.controls.passengerName.setValue('');
    expect(component.form.invalid).toBeTrue();
  });

  it('should mark form valid when all fields are filled', () => {
    component.form.patchValue({
      passengerName: 'Ali',
      routeId: 1,
      vehicleId: 5,
      passengerCount: 3
    });
    expect(component.form.valid).toBeTrue();
  });

  it('should call service on valid submit', () => {
    bookingService.create.and.returnValue(of({ id: 42 }));

    component.form.patchValue({
      passengerName: 'Ali',
      routeId: 1,
      vehicleId: 5,
      passengerCount: 3
    });
    component.onSubmit();

    expect(bookingService.create).toHaveBeenCalledWith(jasmine.objectContaining({
      passengerName: 'Ali',
      routeId: 1,
      vehicleId: 5,
      passengerCount: 3
    }));
  });

  it('should not call service when form is invalid', () => {
    component.form.controls.passengerName.setValue('');
    component.onSubmit();

    expect(bookingService.create).not.toHaveBeenCalled();
  });
});
```
