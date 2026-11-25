using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Queries;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelTravelerTests
{
    private const int Tenant = 5;
    private const int CustomerId = 40;

    private readonly Mock<ITravelCustomerRepository> _customers = new();
    private readonly Mock<ITravelTravelerRepository> _travelers = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelTravelerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
        _customers.Setup(c => c.GetRefAsync(CustomerId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCustomerRef(CustomerId, 2));
        _config.Setup(c => c.CountryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _config.Setup(c => c.GetSettingsAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelSettingsDto());
    }

    private TravelTravelerGuard Guard => new(_customers.Object, _travelers.Object, _config.Object);

    private static SaveTravelTravelerDto Traveler(
        string gender = "Female", string? passport = "ab1234567", int? mahramId = null, string? relation = null,
        DateTime? issue = null, DateTime? expiry = null, string? cnic = null) =>
        new(null, "  aisha ", "khan", gender, new DateTime(1990, 1, 1), null, "pk", passport, "pk",
            issue ?? new DateTime(2022, 1, 1), expiry ?? new DateTime(2032, 1, 1), cnic, null, null, mahramId, relation, null);

    // ── Tenant / branch isolation ──────────────────────────────────────────

    [Fact]
    public async Task Create_for_customer_of_another_tenant_is_not_found_and_never_inserts()
    {
        _customers.Setup(c => c.GetRefAsync(999, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync((TravelCustomerRef?)null);
        var handler = new CreateTravelTravelerCommandHandler(_travelers.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var act = () => handler.Handle(new CreateTravelTravelerCommand(999, Traveler()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _travelers.Verify(t => t.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<SaveTravelTravelerDto>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_for_customer_in_other_branch_is_not_found()
    {
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelBranchScope(false, [1]));
        var handler = new GetTravelTravelersByCustomerQueryHandler(_travelers.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var act = () => handler.Handle(new GetTravelTravelersByCustomerQuery(CustomerId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _travelers.Verify(t => t.GetByCustomerAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_of_traveler_from_another_tenant_is_not_found()
    {
        _travelers.Setup(t => t.GetIdentityAsync(77, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync((TravelTravelerIdentity?)null);
        var handler = new UpdateTravelTravelerCommandHandler(_travelers.Object, _config.Object, Guard, _scope.Object, _tenant.Object);

        var act = () => handler.Handle(new UpdateTravelTravelerCommand(77, Traveler()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _travelers.Verify(t => t.UpdateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<SaveTravelTravelerDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Create / normalisation ─────────────────────────────────────────────

    [Fact]
    public async Task Create_normalises_names_codes_and_passport_and_scopes_to_tenant()
    {
        _travelers.Setup(t => t.CreateAsync(Tenant, CustomerId, It.IsAny<SaveTravelTravelerDto>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(9);
        var handler = new CreateTravelTravelerCommandHandler(_travelers.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new CreateTravelTravelerCommand(CustomerId, Traveler()), CancellationToken.None);

        result.Data.Should().Be(9);
        _travelers.Verify(t => t.CreateAsync(Tenant, CustomerId, It.Is<SaveTravelTravelerDto>(d =>
            d.GivenNames == "AISHA" && d.Surname == "KHAN" && d.PassportNumber == "AB1234567"
            && d.NationalityCode == "PK" && d.PassportIssuingCountryCode == "PK"), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_warns_when_passport_already_expired()
    {
        _travelers.Setup(t => t.CreateAsync(Tenant, CustomerId, It.IsAny<SaveTravelTravelerDto>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(9);
        var handler = new CreateTravelTravelerCommandHandler(_travelers.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new CreateTravelTravelerCommand(CustomerId,
            Traveler(issue: new DateTime(2010, 1, 1), expiry: new DateTime(2020, 1, 1))), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Create_rejects_duplicate_passport()
    {
        _travelers.Setup(t => t.PassportExistsAsync(Tenant, "PK", "AB1234567", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new CreateTravelTravelerCommandHandler(_travelers.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new CreateTravelTravelerCommand(CustomerId, Traveler()), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Normalizer_keeps_stored_identity_for_null_or_masked_and_clears_for_empty()
    {
        var stored = new TravelTravelerIdentity(1, CustomerId, "Female", "AB1234567", "3520212345671");

        TravelTravelerNormalizer.Normalize(Traveler(passport: null), stored).PassportNumber.Should().Be("AB1234567");
        TravelTravelerNormalizer.Normalize(Traveler(passport: TravelPii.Mask("AB1234567")), stored).PassportNumber.Should().Be("AB1234567");
        TravelTravelerNormalizer.Normalize(Traveler(passport: ""), stored).PassportNumber.Should().BeNull();
        TravelTravelerNormalizer.Normalize(Traveler(cnic: "35202-1234567-1"), stored).Cnic.Should().Be("3520212345671");
    }

    // ── Mahram rules ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(41, "Male", "same customer")]   // mahram belongs to another customer
    [InlineData(40, "Female", "male")]          // mahram must be male
    public async Task Mahram_must_be_male_traveler_of_same_customer(int mahramCustomer, string mahramGender, string expected)
    {
        _travelers.Setup(t => t.GetIdentityAsync(8, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelTravelerIdentity(8, mahramCustomer, mahramGender, null, null));

        var error = await Guard.ValidateAsync(Tenant, CustomerId, 3, Traveler(mahramId: 8, relation: "Husband"), CancellationToken.None);

        error.Should().Contain(expected);
    }

    [Fact]
    public async Task Traveler_cannot_be_own_mahram()
    {
        var error = await Guard.ValidateAsync(Tenant, CustomerId, 3, Traveler(mahramId: 3, relation: "Husband"), CancellationToken.None);
        error.Should().Contain("own mahram");
    }

    // ── Presentation ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-12-01", "ExpiringSoon")]
    [InlineData("2026-01-01", "Expired")]
    [InlineData("2030-01-01", "Valid")]
    public void Passport_status_uses_warning_window(string expiry, string expected)
    {
        var today = new DateTime(2026, 11, 25);
        TravelPii.PassportStatus("AB1234567", DateTime.Parse(expiry), today, 180).Should().Be(expected);
    }

    [Fact]
    public void Passport_status_is_missing_without_number()
        => TravelPii.PassportStatus(null, new DateTime(2030, 1, 1), DateTime.UtcNow, 180).Should().Be("Missing");

    [Fact]
    public void Presenter_masks_passport_and_cnic_without_permission()
    {
        var dto = new TravelTravelerDto { PassportNumber = "AB1234567", Cnic = "3520212345671", PassportExpiryDate = new DateTime(2032, 1, 1) };
        var shown = TravelTravelerPresenter.Present(dto, canViewPii: false, new DateTime(2026, 11, 25), 180);

        shown.PassportNumber.Should().Be("••••••567");
        shown.Cnic.Should().Be("•••••••••5671");
        shown.PiiMasked.Should().BeTrue();
        shown.PassportStatus.Should().Be("Valid");
    }

    [Fact]
    public void Presenter_keeps_values_with_permission()
    {
        var dto = new TravelTravelerDto { PassportNumber = "AB1234567", Cnic = "3520212345671" };
        var shown = TravelTravelerPresenter.Present(dto, canViewPii: true, DateTime.UtcNow, 180);
        shown.PassportNumber.Should().Be("AB1234567");
        shown.PiiMasked.Should().BeFalse();
    }

    // ── Validation ─────────────────────────────────────────────────────────

    [Fact]
    public void Create_validator_rejects_masked_passport_and_expiry_before_issue()
    {
        var v = new CreateTravelTravelerCommandValidator();
        var dto = Traveler(passport: TravelPii.Mask("AB1234567"), issue: new DateTime(2024, 1, 1), expiry: new DateTime(2023, 1, 1));
        var errors = v.Validate(new CreateTravelTravelerCommand(CustomerId, dto)).Errors.Select(e => e.PropertyName).ToList();

        errors.Should().Contain("Traveler.PassportNumber");
        errors.Should().Contain("Traveler.PassportExpiryDate");
    }

    [Fact]
    public void Validator_requires_relation_when_mahram_set()
    {
        var v = new CreateTravelTravelerCommandValidator();
        var errors = v.Validate(new CreateTravelTravelerCommand(CustomerId, Traveler(mahramId: 2))).Errors;
        errors.Should().Contain(e => e.PropertyName == "Traveler.MahramRelation");
    }
}
