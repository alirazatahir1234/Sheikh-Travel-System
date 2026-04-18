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
    var booking = await _connection.QuerySingleOrDefaultAsync<BookingResponse>(sql, new { query.Id });

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

// booking.service.ts
@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly apiUrl = '/api/bookings';

  constructor(private http: HttpClient) {}

  getConfirmedBookings(): Observable<Booking[]> {
    return this.http.get<Booking[]>(this.apiUrl, {
      params: { status: 'confirmed' }
    });
  }
}
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

// app.config.ts
export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor]))
  ]
};
```

**Example — Angular: Route guards for protected pages**

```typescript
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};

// app.routes.ts
export const routes: Routes = [
  { path: 'bookings', component: BookingListComponent, canActivate: [authGuard] },
  { path: 'login', component: LoginComponent }
];
```

---

### Performance

* Are database calls efficient?
* Is pagination used where needed?
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

**Example — Angular: trackBy in loops, unsubscribe to prevent leaks**

```typescript
// GOOD — trackBy prevents unnecessary DOM re-renders
@Component({
  selector: 'app-vehicle-list',
  standalone: true,
  imports: [VehicleCardComponent],
  template: `
    @for (vehicle of vehicles; track vehicle.id) {
      <app-vehicle-card [vehicle]="vehicle" />
    }
  `
})
export class VehicleListComponent {}

// GOOD — takeUntilDestroyed prevents memory leaks
export class RouteDetailComponent implements OnInit {
  private destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.routeService.getById(this.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(route => this.route = route);
  }
}
```

---

### Error handling

* Are exceptions handled properly?
* Are meaningful error messages returned?
* Is logging included where needed?
* Is the API response consistent?

**Example — .NET: Global exception handler middleware**

```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found");
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Message = ex.Message,
                StatusCode = 404
            });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Message = "Validation failed.",
                Errors = ex.Errors.Select(e => e.ErrorMessage).ToList(),
                StatusCode = 400
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Message = "An unexpected error occurred.",
                StatusCode = 500
            });
        }
    }
}
```

**Example — Angular: Global error interceptor**

```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        router.navigate(['/login']);
      } else if (error.status === 400) {
        snackBar.open(error.error?.message || 'Invalid request.', 'Close');
      } else {
        snackBar.open('Something went wrong. Please try again.', 'Close');
      }
      return throwError(() => error);
    })
  );
};
```

---

### Testing

* Are unit tests added or updated?
* Do tests cover success and failure cases?
* Are mocks used correctly?
* Are assertions clear and meaningful?

**Example — .NET: xUnit test for a CQRS handler**

```csharp
public class CreateBookingHandlerTests
{
    private readonly Mock<IDbConnection> _connectionMock = new();
    private readonly CreateBookingHandler _handler;

    public CreateBookingHandlerTests()
    {
        _handler = new CreateBookingHandler(_connectionMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsBookingId()
    {
        var command = new CreateBookingCommand
        {
            PassengerName = "Ali",
            RouteId = 1,
            VehicleId = 5,
            PassengerCount = 3
        };

        _connectionMock.SetupDapperAsync(c =>
            c.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(42);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(42, result.Id);
    }

    [Fact]
    public async Task Handle_MissingPassengerName_FailsValidation()
    {
        var command = new CreateBookingCommand { PassengerName = "", RouteId = 1 };
        var validator = new CreateBookingCommandValidator();

        var validationResult = await validator.ValidateAsync(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "PassengerName");
    }
}
```

**Example — Angular: Jasmine test for a service**

```typescript
describe('BookingService', () => {
  let service: BookingService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        BookingService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(BookingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch confirmed bookings', () => {
    const mockBookings: Booking[] = [
      { id: 1, passengerName: 'Ali', status: 'confirmed', totalPrice: 500 }
    ];

    service.getConfirmedBookings().subscribe(bookings => {
      expect(bookings.length).toBe(1);
      expect(bookings[0].passengerName).toBe('Ali');
    });

    const req = httpMock.expectOne(r =>
      r.url === '/api/bookings' && r.params.get('status') === 'confirmed'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockBookings);
  });

  it('should handle server error', () => {
    service.getConfirmedBookings().subscribe({
      error: (err) => expect(err.status).toBe(500)
    });

    const req = httpMock.expectOne(r => r.url === '/api/bookings');
    req.flush('Server error', { status: 500, statusText: 'Internal Server Error' });
  });
});
```

---

## Code review rules

* Review the code, not the person.
* Be specific in comments.
* Suggest improvement, not just criticism.
* Do not approve code you do not understand.
* Do not merge code with unresolved critical issues.
* Keep review comments focused on the current change only.

---

## Review comment types

### Must-fix

Use when the change can break functionality, architecture, security, or data integrity.

**Examples:**

* "This SQL uses string concatenation — must use parameterized queries with Dapper."
* "Business logic in the controller — move to the CQRS handler."
* "This endpoint has no `[Authorize]` attribute — anyone can access it."
* "The Angular component calls `HttpClient` directly — use a service."

### Should-fix

Use when the change is correct but weak in quality, performance, or maintainability.

**Examples:**

* "This query does `SELECT *` — select only the fields used in the DTO."
* "Add `takeUntilDestroyed` to prevent subscription leaks in the component."
* "The handler method is 80 lines — split into smaller private methods."

### Nice-to-have

Use for style improvements, naming, formatting, or optional refactoring.

**Examples:**

* "Consider renaming `data` to `bookingList` for clarity."
* "This could use `async` pipe instead of manual subscription."

---

## Reviewer checklist

Before approving, confirm:

* Business logic is correct
* Validation is complete (FluentValidation on .NET, reactive form validators on Angular)
* No hardcoded secrets or API keys
* No SQL injection risk (all Dapper queries parameterized)
* No broken dependency between layers (controller → handler → repository)
* Tests are added or updated (xUnit for .NET, Jasmine/Karma for Angular)
* Naming is consistent (`PascalCase` for .NET, `camelCase` for Angular/TypeScript)
* Code follows project standards (Clean Architecture, standalone components, typed models)
