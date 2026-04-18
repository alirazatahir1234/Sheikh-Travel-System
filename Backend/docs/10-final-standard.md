# Final Standard

Every code contribution should be:

* **correct** — solves the requirement, handles edge cases
* **simple** — easy to read, no unnecessary complexity
* **secure** — parameterized SQL, validated input, JWT auth, role-based access
* **maintainable** — follows Clean Architecture, proper layer separation
* **testable** — xUnit tests for .NET handlers, Jasmine tests for Angular components
* **consistent** — follows naming conventions, project structure, and coding patterns
* **ready for production** — no debug code, no secrets, no `any` types, no `console.log`

---

## Quick reference — What correct code looks like

### Backend (.NET)

```csharp
// Controller — thin, delegates to MediatR
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public BookingsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetBookingByIdQuery(id));
        return Ok(new ApiResponse<BookingResponse> { Success = true, Data = result });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetBookingsQuery(page, pageSize));
        return Ok(new ApiResponse<PagedResult<BookingListItem>> { Success = true, Data = result });
    }
}

// Handler — focused business logic with Dapper
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
        _logger.LogInformation("Booking {BookingId} created for {PassengerName}", id, command.PassengerName);
        return new BookingCreatedResponse { Id = id };
    }
}

// Validator — FluentValidation
public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.PassengerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RouteId).GreaterThan(0);
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.PassengerCount).InclusiveBetween(1, 50);
    }
}

// Test — xUnit + Moq
[Fact]
public async Task Handle_ValidCommand_ReturnsBookingId()
{
    // Arrange
    var command = new CreateBookingCommand("Ali", RouteId: 1, VehicleId: 5, PassengerCount: 3);
    _connectionMock.SetupDapperAsync(c =>
        c.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
        .ReturnsAsync(42);

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    Assert.Equal(42, result.Id);
}
```

### Frontend (Angular)

```typescript
// Model — typed interface
export interface Booking {
  id: number;
  passengerName: string;
  routeId: number;
  totalPrice: number;
  status: BookingStatus;
}
export type BookingStatus = 'pending' | 'confirmed' | 'cancelled';

// Service — typed HTTP calls
@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly apiUrl = `${environment.apiUrl}/bookings`;
  private http = inject(HttpClient);

  getAll(page = 1, pageSize = 20): Observable<PagedResult<Booking>> {
    return this.http.get<ApiResponse<PagedResult<Booking>>>(this.apiUrl, {
      params: { page: page.toString(), pageSize: pageSize.toString() }
    }).pipe(map(res => res.data));
  }

  create(request: CreateBookingRequest): Observable<BookingCreatedResponse> {
    return this.http.post<ApiResponse<BookingCreatedResponse>>(this.apiUrl, request).pipe(
      map(res => res.data)
    );
  }
}

// Component — standalone, reactive forms, proper error handling
@Component({
  selector: 'app-booking-form',
  standalone: true,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './booking-form.component.html'
})
export class BookingFormComponent {
  private bookingService = inject(BookingService);
  private router = inject(Router);
  loading = false;

  form = new FormGroup({
    passengerName: new FormControl('', [Validators.required, Validators.maxLength(100)]),
    routeId: new FormControl<number | null>(null, [Validators.required]),
    vehicleId: new FormControl<number | null>(null, [Validators.required]),
    passengerCount: new FormControl(1, [Validators.required, Validators.min(1), Validators.max(50)])
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading = true;

    this.bookingService.create(this.form.value as CreateBookingRequest).subscribe({
      next: (res) => this.router.navigate(['/bookings', res.id]),
      error: () => this.loading = false
    });
  }
}

// Test — Jasmine
describe('BookingFormComponent', () => {
  it('should not submit when form is invalid', () => {
    component.form.controls.passengerName.setValue('');
    component.onSubmit();
    expect(bookingService.create).not.toHaveBeenCalled();
  });

  it('should call service on valid submit', () => {
    bookingService.create.and.returnValue(of({ id: 42 }));
    component.form.patchValue({ passengerName: 'Ali', routeId: 1, vehicleId: 5, passengerCount: 3 });
    component.onSubmit();
    expect(bookingService.create).toHaveBeenCalled();
  });
});
```

---

These rules are mandatory for all contributors to **Sheikh Travel System**.
