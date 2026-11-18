using System.Text.RegularExpressions;
using FluentAssertions;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Pass 2, step 2 — CustomerPortal SQL must carry TenantId on tenant-owned roots and joins.
/// The generic guard only checks for a TenantId token; these tests pin the predicates.
/// </summary>
public class Sg01CustomerPortalIsolationTests
{
    private static readonly string Backend = FindBackendRoot();
    private static readonly string RepoBody = File.ReadAllText(
        Directory.EnumerateFiles(Backend, "CustomerPortalRepository.cs", SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/")));

    [Theory]
    [InlineData("GetRoutesAsync", "TenantId = @TenantId")]
    [InlineData("GetVehiclesAsync", "TenantId = @TenantId")]
    [InlineData("GetRouteBasePriceAsync", "TenantId = @TenantId")]
    [InlineData("VehicleExistsAndNotRetiredAsync", "TenantId = @TenantId")]
    [InlineData("GetPerKmBasePriceAsync", "TenantId = @TenantId")]
    [InlineData("GetFirstActiveRouteIdAsync", "TenantId = @TenantId")]
    [InlineData("GetRouteLabelAsync", "TenantId = @TenantId")]
    [InlineData("GetVehicleSeatingCapacityAsync", "TenantId = @TenantId")]
    [InlineData("GetVehicleLabelAsync", "TenantId = @TenantId")]
    [InlineData("GetBookingNumberAsync", "TenantId = @TenantId")]
    [InlineData("GetBookingStatusVehicleAsync", "TenantId = @TenantId")]
    [InlineData("GetBookingStatusPickupAsync", "TenantId = @TenantId")]
    [InlineData("CancelBookingAsync", "TenantId = @TenantId")]
    public void Catalog_and_booking_roots_filter_TenantId(string methodName, string expected)
    {
        Normalize(MethodBody(methodName)).Should().Contain(expected);
    }

    [Fact]
    public void Booking_cards_scope_bookings_and_route_join()
    {
        var sql = Normalize(MethodBody("GetBookingCardsAsync"));
        sql.Should().Contain("b.TenantId = @TenantId");
        sql.Should().Contain("r.TenantId = b.TenantId");
    }

    [Fact]
    public void Booking_detail_scopes_route_vehicle_and_driver_joins()
    {
        var sql = Normalize(MethodBody("GetBookingDetailHeadAsync"));
        sql.Should().Contain("b.TenantId = @TenantId");
        sql.Should().Contain("r.TenantId = b.TenantId");
        sql.Should().Contain("v.TenantId = b.TenantId");
        sql.Should().Contain("d.TenantId = b.TenantId");
    }

    [Fact]
    public void Payments_and_seats_join_through_tenant_booking()
    {
        Normalize(MethodBody("GetBookingPaymentsAsync"))
            .Should().Contain("b.TenantId = @TenantId");
        Normalize(MethodBody("GetBookingSeatsAsync"))
            .Should().Contain("b.TenantId = @TenantId");
    }

    [Fact]
    public void Extras_and_seat_taken_require_booking_tenant()
    {
        Normalize(MethodBody("ApplyPortalBookingExtrasAsync"))
            .Should().Contain("WHERE Id = @Id AND TenantId = @TenantId");
        Normalize(MethodBody("IsSeatTakenAsync"))
            .Should().Contain("b.TenantId = @TenantId");
        Normalize(MethodBody("GetBookedSeatsNearPickupAsync"))
            .Should().Contain("b.TenantId = @TenantId");
    }

    [Fact]
    public void Invoice_checkout_and_driver_phone_keep_joined_rows_in_tenant()
    {
        var invoice = Normalize(MethodBody("GetInvoiceRowAsync"));
        invoice.Should().Contain("b.TenantId = @TenantId");
        invoice.Should().Contain("c.TenantId = b.TenantId");
        invoice.Should().Contain("r.TenantId = b.TenantId");

        Normalize(MethodBody("GetCheckoutBookingAsync"))
            .Should().Contain("b.TenantId = @TenantId");
        Normalize(MethodBody("GetStartedBookingDriverPhoneAsync"))
            .Should().Contain("d.TenantId = b.TenantId");
    }

    [Fact]
    public void Customer_children_and_vehicle_helpers_join_parent_tenant()
    {
        Normalize(MethodBody("GetSavedAddressesAsync")).Should().Contain("c.TenantId = @TenantId");
        Normalize(MethodBody("GetCustomerNotificationsAsync")).Should().Contain("c.TenantId = @TenantId");
        Normalize(MethodBody("GetLoyaltyAsync")).Should().Contain("c.TenantId = @TenantId");
        Normalize(MethodBody("GetWalletBalanceAsync")).Should().Contain("c.TenantId = @TenantId");
        Normalize(MethodBody("GetFavoriteRoutesAsync")).Should().Contain("c.TenantId = @TenantId");
        Normalize(MethodBody("GetVehicleLiveLocationAsync")).Should().Contain("v.TenantId = @TenantId");
        Normalize(MethodBody("GetVehicleSeatLayoutsAsync")).Should().Contain("v.TenantId = @TenantId");
    }

    [Fact]
    public void NormalizeCustomerPhones_is_platform_scope_startup_job()
    {
        var declaration = Regex.Match(
            RepoBody,
            @"//\s*PLATFORM-SCOPE:[^\n]*\n\s*public async Task NormalizeCustomerPhonesAsync");
        declaration.Success.Should().BeTrue();
    }

    private static string MethodBody(string methodName)
    {
        var declaration = Regex.Match(RepoBody, $@"\bTask\b[^\n;=]*\s{Regex.Escape(methodName)}\s*\(");
        declaration.Success.Should().BeTrue(because: $"CustomerPortalRepository must still define {methodName}");
        var start = declaration.Index;

        var open = RepoBody.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < RepoBody.Length; i++)
        {
            if (RepoBody[i] == '{') depth++;
            else if (RepoBody[i] == '}' && --depth == 0)
                return RepoBody[open..(i + 1)];
        }
        throw new InvalidOperationException($"Unbalanced braces in CustomerPortalRepository::{methodName}");
    }

    private static string Normalize(string s) => Regex.Replace(s, @"\s+", " ");

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(Path.Combine(backend, "SheikhTravelSystem.Infrastructure")))
                return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend/ root.");
    }
}
