# Project-Specific Rules for Sheikh Travel System

## Backend — .NET 10 with Clean Architecture

### Tech stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 10, ASP.NET Core Web API |
| Architecture | Clean Architecture + CQRS (MediatR) |
| Database access | Dapper (raw SQL, parameterized) |
| Validation | FluentValidation |
| Authentication | JWT Bearer tokens |
| Authorization | Role-based (`[Authorize(Roles = "...")]`) |
| Testing | xUnit + Moq |
| Logging | ILogger / Serilog |

### Project structure

```
src/
├── SheikhTravel.Api/
│   ├── Controllers/
│   │   ├── BookingsController.cs
│   │   ├── VehiclesController.cs
│   │   ├── RoutesController.cs
│   │   └── DriversController.cs
│   ├── Middleware/
│   │   └── GlobalExceptionMiddleware.cs
│   └── Program.cs
│
├── SheikhTravel.Application/
│   ├── Bookings/
│   │   ├── Commands/
│   │   │   ├── CreateBookingCommand.cs
│   │   │   └── CreateBookingHandler.cs
│   │   ├── Queries/
│   │   │   ├── GetBookingByIdQuery.cs
│   │   │   ├── GetBookingByIdHandler.cs
│   │   │   ├── GetBookingsQuery.cs
│   │   │   └── GetBookingsHandler.cs
│   │   ├── Validators/
│   │   │   └── CreateBookingCommandValidator.cs
│   │   └── DTOs/
│   │       ├── BookingResponse.cs
│   │       ├── BookingListItem.cs
│   │       └── BookingCreatedResponse.cs
│   ├── Vehicles/
│   ├── Routes/
│   ├── Pricing/
│   └── Common/
│       ├── DTOs/
│       │   ├── ApiResponse.cs
│       │   ├── ApiErrorResponse.cs
│       │   └── PagedResult.cs
│       └── Exceptions/
│           ├── NotFoundException.cs
│           └── BusinessRuleException.cs
│
├── SheikhTravel.Domain/
│   ├── Entities/
│   │   ├── Booking.cs
│   │   ├── Vehicle.cs
│   │   ├── Route.cs
│   │   └── Driver.cs
│   └── Enums/
│       ├── BookingStatus.cs
│       └── VehicleType.cs
│
├── SheikhTravel.Infrastructure/
│   ├── Database/
│   │   └── DapperConnectionFactory.cs
│   └── Services/
│       └── (external integrations)
│
└── tests/
    └── SheikhTravel.Application.Tests/
        ├── Bookings/
        │   ├── CreateBookingHandlerTests.cs
        │   └── CreateBookingCommandValidatorTests.cs
        └── Pricing/
            └── CalculateBookingPriceHandlerTests.cs
```

### Backend rules with examples

**1. Keep CQRS handlers thin — one responsibility per handler**

```csharp
// GOOD — handler does one thing
public class CreateBookingHandler : IRequestHandler<CreateBookingCommand, BookingCreatedResponse>
{
    private readonly IDbConnection _connection;

    public async Task<BookingCreatedResponse> Handle(CreateBookingCommand command, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO Bookings (PassengerName, RouteId, VehicleId, PassengerCount, Status, CreatedAt)
            VALUES (@PassengerName, @RouteId, @VehicleId, @PassengerCount, 'Pending', GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        var id = await _connection.ExecuteScalarAsync<int>(sql, command);
        return new BookingCreatedResponse { Id = id };
    }
}

// BAD — handler does too many things (create + price calc + notification)
public class CreateBookingHandler : IRequestHandler<CreateBookingCommand, BookingCreatedResponse>
{
    public async Task<BookingCreatedResponse> Handle(CreateBookingCommand command, CancellationToken ct)
    {
        // calculate price
        // insert booking
        // send email notification
        // update vehicle availability
        // ... 200 lines of mixed concerns
    }
}
```

**2. Never write business logic in controllers**

```csharp
// GOOD
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
{
    var result = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}

// BAD
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
{
    if (request.PassengerCount > 50) return BadRequest("Too many passengers"); // ❌ Logic in controller
    var price = request.Distance * ratePerKm; // ❌ Calculation in controller
}
```

**3. Use Dapper with parameterized SQL only**

```csharp
// GOOD
const string sql = """
    SELECT Id, Name, Capacity, RatePerKm, FuelAverage
    FROM Vehicles
    WHERE Type = @VehicleType AND IsActive = 1
    ORDER BY Name
    """;
var vehicles = await _connection.QueryAsync<VehicleDto>(sql, new { VehicleType = type });

// BAD
var sql = $"SELECT * FROM Vehicles WHERE Type = '{type}'"; // ❌ SQL injection
```

**4. Validate booking, vehicle, route, and pricing data**

```csharp
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

public class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 60);
        RuleFor(x => x.RatePerKm).GreaterThan(0);
        RuleFor(x => x.FuelAverage).GreaterThan(0).WithMessage("Fuel average must be greater than zero.");
    }
}

public class CreateRouteCommandValidator : AbstractValidator<CreateRouteCommand>
{
    public CreateRouteCommandValidator()
    {
        RuleFor(x => x.Origin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DistanceKm).GreaterThan(0);
        RuleFor(x => x.Origin).NotEqual(x => x.Destination).WithMessage("Origin and destination cannot be the same.");
    }
}
```

**5. Protect APIs with JWT and roles**

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Controller — role-based access
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Operator")]
    public async Task<IActionResult> GetAll([FromQuery] GetBookingsQuery query) { ... }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command) { ... }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancel(int id) { ... }
}
```

**6. Keep pricing logic deterministic and testable**

```csharp
// Pure calculation — easy to test
public class CalculateBookingPriceHandler : IRequestHandler<CalculateBookingPriceQuery, PriceResponse>
{
    private readonly IDbConnection _connection;

    public async Task<PriceResponse> Handle(CalculateBookingPriceQuery query, CancellationToken ct)
    {
        const string routeSql = "SELECT DistanceKm FROM Routes WHERE Id = @RouteId";
        const string vehicleSql = "SELECT RatePerKm, FuelAverage FROM Vehicles WHERE Id = @VehicleId";

        var route = await _connection.QuerySingleOrDefaultAsync<RouteDto>(routeSql, new { query.RouteId })
            ?? throw new NotFoundException($"Route {query.RouteId} not found.");

        var vehicle = await _connection.QuerySingleOrDefaultAsync<VehicleDto>(vehicleSql, new { query.VehicleId })
            ?? throw new NotFoundException($"Vehicle {query.VehicleId} not found.");

        var basePrice = route.DistanceKm * vehicle.RatePerKm;
        var fuelCost = (route.DistanceKm / vehicle.FuelAverage) * query.FuelPricePerLitre;
        var totalPrice = basePrice + fuelCost;

        return new PriceResponse
        {
            BasePrice = basePrice,
            FuelCost = fuelCost,
            TotalPrice = totalPrice
        };
    }
}

// Test
[Theory]
[InlineData(100, 15, 10, 50, 1500, 200, 1700)]  // 100km × 15rate + (100/10 × 50fuel)
[InlineData(200, 20, 8, 60, 4000, 1500, 5500)]
public async Task Handle_CalculatesCorrectPrice(
    decimal distance, decimal rate, decimal fuelAvg, decimal fuelPrice,
    decimal expectedBase, decimal expectedFuel, decimal expectedTotal)
{
    // ... assert all three values
}
```

---

## Frontend — Angular with standalone components

### Tech stack

| Concern | Technology |
|---------|-----------|
| Framework | Angular 19+ (standalone components) |
| Forms | Reactive Forms |
| UI | Angular Material |
| HTTP | HttpClient with functional interceptors |
| State | Services with BehaviorSubject (simple state) |
| Routing | Lazy-loaded feature routes with guards |
| Testing | Jasmine + Karma |
| Linting | ESLint |

### Project structure

```
src/app/
├── core/
│   ├── interceptors/
│   │   ├── auth.interceptor.ts
│   │   └── error.interceptor.ts
│   ├── guards/
│   │   ├── auth.guard.ts
│   │   └── role.guard.ts
│   ├── services/
│   │   └── auth.service.ts
│   └── models/
│       └── api-response.model.ts
│
├── shared/
│   ├── components/
│   │   ├── spinner/
│   │   ├── error-message/
│   │   ├── confirm-dialog/
│   │   └── pagination/
│   ├── pipes/
│   │   └── currency-format.pipe.ts
│   └── models/
│       └── paged-result.model.ts
│
├── features/
│   ├── bookings/
│   │   ├── components/
│   │   │   ├── booking-list/
│   │   │   ├── booking-form/
│   │   │   └── booking-detail/
│   │   ├── models/
│   │   │   └── booking.model.ts
│   │   ├── services/
│   │   │   └── booking.service.ts
│   │   └── booking.routes.ts
│   ├── vehicles/
│   ├── routes/
│   ├── drivers/
│   └── pricing/
│
├── app.component.ts
├── app.config.ts
└── app.routes.ts
```

### Frontend rules with examples

**1. All components must be standalone**

```typescript
// GOOD
@Component({
  selector: 'app-booking-list',
  standalone: true,
  imports: [BookingCardComponent, PaginationComponent, SpinnerComponent],
  templateUrl: './booking-list.component.html'
})
export class BookingListComponent {}

// BAD — using NgModule-based component
@NgModule({
  declarations: [BookingListComponent],
  imports: [CommonModule]
})
export class BookingModule {} // ❌ Not standalone
```

**2. Use typed models — never use `any`**

```typescript
// GOOD
export interface Booking {
  id: number;
  passengerName: string;
  routeId: number;
  vehicleId: number;
  totalPrice: number;
  status: BookingStatus;
  createdAt: string;
}

export type BookingStatus = 'pending' | 'confirmed' | 'cancelled';

// BAD
booking: any; // ❌
```

**3. Use `environment.ts` for API URLs — never hardcode**

```typescript
// environment.ts
export const environment = {
  production: false,
  apiUrl: 'https://localhost:5001/api'
};

// environment.prod.ts
export const environment = {
  production: true,
  apiUrl: 'https://api.sheikhtravel.com/api'
};

// service
private readonly apiUrl = `${environment.apiUrl}/bookings`; // ✅

// BAD
private readonly apiUrl = 'https://localhost:5001/api/bookings'; // ❌
```

**4. Use reactive forms with validation for all user input**

```typescript
form = new FormGroup({
  passengerName: new FormControl('', [Validators.required, Validators.maxLength(100)]),
  routeId: new FormControl<number | null>(null, [Validators.required]),
  vehicleId: new FormControl<number | null>(null, [Validators.required]),
  passengerCount: new FormControl(1, [Validators.required, Validators.min(1), Validators.max(50)])
});
```

**5. Use lazy-loaded routes with guards**

```typescript
// app.routes.ts
export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: 'bookings',
    canActivate: [authGuard],
    loadChildren: () => import('./features/bookings/booking.routes').then(m => m.bookingRoutes)
  },
  {
    path: 'vehicles',
    canActivate: [authGuard],
    loadChildren: () => import('./features/vehicles/vehicle.routes').then(m => m.vehicleRoutes)
  },
  { path: '', redirectTo: 'bookings', pathMatch: 'full' },
  { path: '**', component: NotFoundComponent }
];

// booking.routes.ts
export const bookingRoutes: Routes = [
  { path: '', component: BookingListComponent },
  { path: 'new', component: BookingFormComponent, canActivate: [roleGuard('Admin', 'Manager')] },
  { path: ':id', component: BookingDetailComponent }
];
```

**6. Use `inject()` function for dependency injection**

```typescript
// GOOD — modern inject() pattern
export class BookingListComponent {
  private bookingService = inject(BookingService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
}

// Also acceptable — constructor injection
export class BookingListComponent {
  constructor(
    private bookingService: BookingService,
    private router: Router
  ) {}
}
```
