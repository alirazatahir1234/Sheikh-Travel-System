using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Queries;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelEnquiryHandlerTests
{
    private const int Tenant = 4;

    private readonly Mock<ITravelEnquiryRepository> _enquiries = new();
    private readonly Mock<ITravelCustomerRepository> _customers = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelEnquiryHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _user.Setup(u => u.UserId).Returns(3);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
        _config.Setup(c => c.CountryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private TravelEnquiryGuard Guard => new(_enquiries.Object, _customers.Object, _config.Object);

    private static SaveTravelEnquiryDto Lead(int? customerId = null, int? visaTypeId = null, string? country = null, int? branchId = null) =>
        new(customerId, customerId is null ? "Bilal Ahmed" : null, customerId is null ? "+92 300 1112223" : null, null,
            "Visa", country, visaTypeId, null, null, 2, 0, 0, null, null, null, null, branchId, "Normal", null, null);

    private void GivenEnquiry(int id, string status, int? customerId = null, int? branchId = null, int? visaTypeId = null)
        => _enquiries.Setup(e => e.GetByIdAsync(id, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelEnquiryDto
            {
                Id = id, Status = status, CustomerId = customerId, BranchId = branchId, VisaTypeId = visaTypeId,
                LeadName = customerId is null ? "Bilal Ahmed" : null, LeadPhone = customerId is null ? "03001112223" : null
            });

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_for_lead_scopes_to_tenant()
    {
        _enquiries.Setup(e => e.CreateAsync(Tenant, It.IsAny<SaveTravelEnquiryDto>(), 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);
        var handler = new CreateTravelEnquiryCommandHandler(_enquiries.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new CreateTravelEnquiryCommand(Lead()), CancellationToken.None);

        result.Data.Should().Be(15);
    }

    [Fact]
    public async Task Create_for_customer_of_another_tenant_is_not_found()
    {
        _customers.Setup(c => c.GetRefAsync(77, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync((TravelCustomerRef?)null);
        var handler = new CreateTravelEnquiryCommandHandler(_enquiries.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var act = () => handler.Handle(new CreateTravelEnquiryCommand(Lead(customerId: 77)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_inherits_branch_from_customer_and_country_from_visa_type()
    {
        _customers.Setup(c => c.GetRefAsync(10, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelCustomerRef(10, 6));
        _config.Setup(c => c.GetVisaTypeByIdAsync(2, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaTypeDto { Id = 2, CountryCode = "SA", IsActive = true });
        var handler = new CreateTravelEnquiryCommandHandler(_enquiries.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        await handler.Handle(new CreateTravelEnquiryCommand(Lead(customerId: 10, visaTypeId: 2)), CancellationToken.None);

        _enquiries.Verify(e => e.CreateAsync(Tenant, It.Is<SaveTravelEnquiryDto>(d => d.BranchId == 6 && d.DestinationCountryCode == "SA"),
            It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_rejects_visa_type_of_a_different_country()
    {
        _config.Setup(c => c.GetVisaTypeByIdAsync(2, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaTypeDto { Id = 2, CountryCode = "SA", IsActive = true });
        var handler = new CreateTravelEnquiryCommandHandler(_enquiries.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new CreateTravelEnquiryCommand(Lead(visaTypeId: 2, country: "AE")), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("different country");
    }

    [Fact]
    public void Validator_requires_lead_details_without_customer_and_currency_with_budget()
    {
        var v = new CreateTravelEnquiryCommandValidator();
        var dto = Lead() with { LeadName = null, LeadPhone = null, Budget = 500_000m, CurrencyCode = null };
        var props = v.Validate(new CreateTravelEnquiryCommand(dto)).Errors.Select(e => e.PropertyName).ToList();
        props.Should().Contain(["Enquiry.LeadName", "Enquiry.LeadPhone", "Enquiry.CurrencyCode"]);
    }

    // ── Status / lost ──────────────────────────────────────────────────────

    [Fact]
    public async Task Status_change_not_allowed_from_current_status_is_refused_without_writing()
    {
        GivenEnquiry(5, "Won", customerId: 1);
        var handler = new ChangeTravelEnquiryStatusCommandHandler(_enquiries.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new ChangeTravelEnquiryStatusCommand(5, EnquiryStateMachine.Actions.MarkQuoted, null), CancellationToken.None);

        result.Success.Should().BeFalse();
        _enquiries.Verify(e => e.ChangeStatusAsync(It.IsAny<int>(), It.IsAny<TravelEnquiryStatusChange>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Concurrent_status_change_raises_conflict()
    {
        GivenEnquiry(5, "New");
        _enquiries.Setup(e => e.ChangeStatusAsync(Tenant, It.IsAny<TravelEnquiryStatusChange>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new ChangeTravelEnquiryStatusCommandHandler(_enquiries.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var act = () => handler.Handle(new ChangeTravelEnquiryStatusCommand(5, EnquiryStateMachine.Actions.MarkContacted, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Lost_requires_a_reason_of_this_tenant()
    {
        GivenEnquiry(5, "Quoted");
        _config.Setup(c => c.GetLostReasonByIdAsync(9, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync((TravelLostReasonDto?)null);
        var handler = new MarkTravelEnquiryLostCommandHandler(_enquiries.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new MarkTravelEnquiryLostCommand(5, new MarkTravelEnquiryLostDto(9, null)), CancellationToken.None);

        result.Success.Should().BeFalse();
        _enquiries.Verify(e => e.ChangeStatusAsync(It.IsAny<int>(), It.IsAny<TravelEnquiryStatusChange>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Lost_records_reason_and_expected_status()
    {
        GivenEnquiry(5, "Quoted");
        _config.Setup(c => c.GetLostReasonByIdAsync(9, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelLostReasonDto { Id = 9, Name = "Price" });
        _enquiries.Setup(e => e.ChangeStatusAsync(Tenant, It.IsAny<TravelEnquiryStatusChange>(), 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new MarkTravelEnquiryLostCommandHandler(_enquiries.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        await handler.Handle(new MarkTravelEnquiryLostCommand(5, new MarkTravelEnquiryLostDto(9, "Chose cheaper agent")), CancellationToken.None);

        _enquiries.Verify(e => e.ChangeStatusAsync(Tenant, It.Is<TravelEnquiryStatusChange>(c =>
            c.ExpectedStatus == "Quoted" && c.NewStatus == "Lost" && c.LostReasonId == 9 && c.LostNotes == "Chose cheaper agent"),
            3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Lost_validator_requires_reason()
        => new MarkTravelEnquiryLostCommandValidator()
            .Validate(new MarkTravelEnquiryLostCommand(5, new MarkTravelEnquiryLostDto(0, null)))
            .IsValid.Should().BeFalse();

    // ── Activity ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("New", "Outbound", true)]
    [InlineData("New", "Inbound", false)]
    [InlineData("Qualified", "Outbound", false)]
    public async Task Outbound_first_contact_promotes_new_enquiry(string status, string direction, bool promote)
    {
        GivenEnquiry(5, status);
        _enquiries.Setup(e => e.LogActivityAsync(Tenant, 5, It.IsAny<LogTravelEnquiryActivityDto>(), It.IsAny<DateTime>(), It.IsAny<bool>(),
            3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new LogTravelEnquiryActivityCommandHandler(_enquiries.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        await handler.Handle(new LogTravelEnquiryActivityCommand(5,
            new LogTravelEnquiryActivityDto("Call", direction, "Discussed Umrah dates", null, null, null)), CancellationToken.None);

        _enquiries.Verify(e => e.LogActivityAsync(Tenant, 5, It.IsAny<LogTravelEnquiryActivityDto>(), It.IsAny<DateTime>(), promote,
            3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Users_cannot_log_system_status_change_activities()
        => new LogTravelEnquiryActivityCommandValidator()
            .Validate(new LogTravelEnquiryActivityCommand(5, new LogTravelEnquiryActivityDto("StatusChange", "Internal", "x", null, null, null)))
            .IsValid.Should().BeFalse();

    // ── Convert ────────────────────────────────────────────────────────────

    private ConvertTravelEnquiryCommandHandler ConvertHandler()
        => new(_enquiries.Object, _customers.Object, _config.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    [Fact]
    public async Task Convert_lead_creates_customer_and_visa_case_in_one_plan()
    {
        GivenEnquiry(5, "Quoted", branchId: 2, visaTypeId: 8);
        _config.Setup(c => c.GetVisaTypeByIdAsync(8, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaTypeDto { Id = 8, CountryCode = "SA", IsActive = true });
        _enquiries.Setup(e => e.ConvertAsync(Tenant, It.IsAny<TravelEnquiryConversionPlan>(), 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelEnquiryConversionResult(100, 200, "VC-2026-00001"));

        var result = await ConvertHandler().Handle(new ConvertTravelEnquiryCommand(5,
            new ConvertTravelEnquiryDto(null, "Individual", true, null, null, null)), CancellationToken.None);

        result.Data!.VisaCaseNumber.Should().Be("VC-2026-00001");
        _enquiries.Verify(e => e.ConvertAsync(Tenant, It.Is<TravelEnquiryConversionPlan>(p =>
            p.ExpectedStatus == "Quoted" && p.CustomerId == null &&
            p.NewCustomer!.FullName == "Bilal Ahmed" && p.NewCustomer.BranchId == 2 &&
            p.VisaCase!.CountryCode == "SA" && p.VisaCase.VisaTypeId == 8 && p.VisaCase.BranchId == 2),
            3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Convert_with_existing_customer_uses_customer_branch_for_visa_case()
    {
        GivenEnquiry(5, "Contacted", branchId: 2);
        _customers.Setup(c => c.GetRefAsync(40, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelCustomerRef(40, 7));
        _config.Setup(c => c.GetVisaTypeByIdAsync(8, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaTypeDto { Id = 8, CountryCode = "GB", IsActive = true });
        _enquiries.Setup(e => e.ConvertAsync(Tenant, It.IsAny<TravelEnquiryConversionPlan>(), 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelEnquiryConversionResult(40, 201, "VC-2026-00002"));

        await ConvertHandler().Handle(new ConvertTravelEnquiryCommand(5,
            new ConvertTravelEnquiryDto(40, null, true, "GB", 8, null)), CancellationToken.None);

        _enquiries.Verify(e => e.ConvertAsync(Tenant, It.Is<TravelEnquiryConversionPlan>(p =>
            p.CustomerId == 40 && p.NewCustomer == null && p.VisaCase!.BranchId == 7), 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Convert_refuses_closed_enquiry()
    {
        GivenEnquiry(5, "Lost");
        var result = await ConvertHandler().Handle(new ConvertTravelEnquiryCommand(5,
            new ConvertTravelEnquiryDto(null, null, false, null, null, null)), CancellationToken.None);

        result.Success.Should().BeFalse();
        _enquiries.Verify(e => e.ConvertAsync(It.IsAny<int>(), It.IsAny<TravelEnquiryConversionPlan>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Convert_with_existing_customer_of_other_tenant_is_not_found()
    {
        GivenEnquiry(5, "Quoted");
        _customers.Setup(c => c.GetRefAsync(999, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync((TravelCustomerRef?)null);

        var act = () => ConvertHandler().Handle(new ConvertTravelEnquiryCommand(5,
            new ConvertTravelEnquiryDto(999, null, false, null, null, null)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Convert_concurrent_change_raises_conflict()
    {
        GivenEnquiry(5, "Quoted");
        _enquiries.Setup(e => e.ConvertAsync(Tenant, It.IsAny<TravelEnquiryConversionPlan>(), 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TravelEnquiryConversionResult?)null);

        var act = () => ConvertHandler().Handle(new ConvertTravelEnquiryCommand(5,
            new ConvertTravelEnquiryDto(null, null, false, null, null, null)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    // ── Isolation / queries ────────────────────────────────────────────────

    [Fact]
    public async Task Get_by_id_for_other_tenant_or_branch_is_not_found()
    {
        _enquiries.Setup(e => e.GetByIdAsync(5, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TravelEnquiryDto?)null);
        var handler = new GetTravelEnquiryByIdQueryHandler(_enquiries.Object, _scope.Object, _tenant.Object);

        var act = () => handler.Handle(new GetTravelEnquiryByIdQuery(5), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _enquiries.Verify(e => e.GetActivitiesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Summary_counts_cover_every_status_and_sum_to_total()
    {
        var summary = GetTravelEnquirySummaryQueryHandler.Build(new Dictionary<string, int> { ["New"] = 4, ["Won"] = 2, ["Lost"] = 1 }, 3);

        summary.Counts.Keys.Should().BeEquivalentTo(TravelValues.EnquiryStatuses);
        summary.Counts.Values.Sum().Should().Be(summary.Total);
        summary.Total.Should().Be(7);
        summary.Counts["Quoted"].Should().Be(0);
    }

    [Fact]
    public void List_rejects_inverted_date_range()
        => new GetTravelEnquiriesQueryValidator()
            .Validate(new GetTravelEnquiriesQuery { CreatedFrom = new DateTime(2026, 11, 10), CreatedTo = new DateTime(2026, 11, 1) })
            .IsValid.Should().BeFalse();
}
