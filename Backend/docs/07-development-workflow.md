# Recommended Development Workflow

## Step-by-step workflow

### 1. Create a feature branch

```bash
# Backend
git checkout -b feature/backend-create-booking

# Frontend
git checkout -b feature/frontend-booking-form
```

### 2. Implement one focused change

**Backend (.NET) — Example: Create Booking feature**

Create these files in order:

```
SheikhTravel.Application/
├── Bookings/
│   ├── Commands/
│   │   ├── CreateBookingCommand.cs
│   │   └── CreateBookingHandler.cs
│   ├── Validators/
│   │   └── CreateBookingCommandValidator.cs
│   └── DTOs/
│       ├── CreateBookingRequest.cs
│       └── BookingCreatedResponse.cs

SheikhTravel.Api/
└── Controllers/
    └── BookingsController.cs  (add POST endpoint)
```

```csharp
// 1. Command
public record CreateBookingCommand(
    string PassengerName,
    int RouteId,
    int VehicleId,
    int PassengerCount
) : IRequest<BookingCreatedResponse>;

// 2. Handler
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

// 3. Validator
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

// 4. Controller endpoint
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
{
    var result = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

**Frontend (Angular) — Example: Booking Form feature**

Create these files in order:

```
src/app/features/bookings/
├── models/
│   └── booking.model.ts
├── services/
│   └── booking.service.ts
├── components/
│   └── booking-form/
│       ├── booking-form.component.ts
│       ├── booking-form.component.html
│       └── booking-form.component.spec.ts
└── booking.routes.ts
```

```typescript
// 1. Model
export interface CreateBookingRequest {
  passengerName: string;
  routeId: number;
  vehicleId: number;
  passengerCount: number;
}

export interface BookingCreatedResponse {
  id: number;
}

// 2. Service
@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly apiUrl = `${environment.apiUrl}/bookings`;
  private http = inject(HttpClient);

  create(request: CreateBookingRequest): Observable<BookingCreatedResponse> {
    return this.http.post<ApiResponse<BookingCreatedResponse>>(this.apiUrl, request).pipe(
      map(res => res.data)
    );
  }
}

// 3. Component
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

// 4. Route
export const bookingRoutes: Routes = [
  { path: 'new', component: BookingFormComponent, canActivate: [authGuard] }
];
```

### 3. Write or update tests

**Backend:**

```csharp
[Fact]
public async Task Handle_ValidCommand_ReturnsBookingId()
{
    // Arrange, Act, Assert pattern
}

[Fact]
public async Task Validator_EmptyName_ReturnsError()
{
    // Validation failure test
}
```

**Frontend:**

```typescript
it('should submit valid form', () => { ... });
it('should not submit invalid form', () => { ... });
it('should show validation errors', () => { ... });
```

### 4. Run local validation

```bash
# Backend
dotnet build && dotnet test

# Frontend
ng build && ng test --watch=false && ng lint
```

### 5. Create PR with clear description

Follow the format in [02-create-pull-request-guidelines.md](02-create-pull-request-guidelines.md).

### 6. Fix review comments

Follow the process in [03-update-pull-request-guidelines.md](03-update-pull-request-guidelines.md).

### 7. Re-test after updates

```bash
# Backend
dotnet build && dotnet test

# Frontend
ng build && ng test --watch=false && ng lint
```

### 8. Merge after approval

Follow the rules in [06-team-merge-rules.md](06-team-merge-rules.md).
