using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;
using A = SheikhTravelSystem.Application.Features.TravelNexus.Groups.TravelGroupStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelGroupHandlerTests
{
    private const int Tenant = 9;
    private const int GroupId = 60;
    private const int PackageId = 5;

    private readonly Mock<ITravelGroupRepository> _groups = new();
    private readonly Mock<ITravelPackageRepository> _packages = new();
    private readonly Mock<ITravelCustomerRepository> _customers = new();
    private readonly Mock<ITravelTravelerRepository> _travelers = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelVisaCaseRepository> _visa = new();
    private readonly Mock<IAuditEngine> _audit = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelGroupHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _user.Setup(u => u.UserId).Returns(7);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
        _packages.Setup(p => p.GetByIdAsync(PackageId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Package("Published"));
    }

    private static TravelPackageDto Package(string status, int? visaTypeId = 3) => new()
    {
        Id = PackageId, Status = status, DurationNights = 14, CurrencyCode = "PKR", VisaTypeId = visaTypeId,
        Stays = [new TravelPackageStayDto { HotelId = 1, Nights = 10, Sequence = 1 }],
        Prices = [new TravelPackagePriceDto { RoomType = "Quad", AdultPrice = 300_000, ChildPrice = 250_000, InfantPrice = 50_000 }]
    };

    private TravelGroupGuard Guard => new(_groups.Object);

    private void GivenGroup(string status, int? visaCaseId = null, int capacity = 10)
        => _groups.Setup(g => g.GetStateAsync(GroupId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelGroupState(GroupId, status, PackageId, capacity, new DateTime(2027, 1, 10), new DateTime(2027, 1, 24), visaCaseId, null, "Jan group"));

    private void GivenMembers(params TravelGroupMemberFacts[] members)
        => _groups.Setup(g => g.GetMemberFactsAsync(GroupId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(members);

    private TransitionTravelGroupCommandHandler TransitionHandler()
        => new(_groups.Object, _packages.Object, _config.Object, _visa.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_requires_a_published_package_and_copies_stays()
    {
        var handler = new CreateTravelGroupCommandHandler(_groups.Object, _packages.Object, _customers.Object, _scope.Object, _tenant.Object, _user.Object);
        var dto = new CreateTravelGroupDto(PackageId, "Jan group", DateTime.UtcNow.Date.AddDays(30), 40, null, null);

        _packages.Setup(p => p.GetByIdAsync(PackageId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Package("Draft"));
        (await handler.Handle(new CreateTravelGroupCommand(dto), CancellationToken.None)).Success.Should().BeFalse();

        _packages.Setup(p => p.GetByIdAsync(PackageId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Package("Published"));
        _groups.Setup(g => g.CreateAsync(Tenant, It.IsAny<CreateTravelGroupDto>(), dto.DepartureDate.AddDays(14),
            It.Is<IReadOnlyList<TravelGroupStayPlan>>(s => s.Count == 1 && s[0].CheckOut == dto.DepartureDate.AddDays(10)), 7,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(GroupId);
        (await handler.Handle(new CreateTravelGroupCommand(dto), CancellationToken.None)).Data.Should().Be(GroupId);
    }

    // ── Register ───────────────────────────────────────────────────────────

    private RegisterTravelGroupMembersCommandHandler RegisterHandler()
        => new(_groups.Object, _packages.Object, _customers.Object, _travelers.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    private void GivenCustomerWithTravelers(params int[] travelerIds)
    {
        _customers.Setup(c => c.GetRefAsync(30, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelCustomerRef(30, null));
        _travelers.Setup(t => t.GetByCustomerAsync(30, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(travelerIds.Select(id => new TravelTravelerDto { Id = id }).ToList());
        _groups.Setup(g => g.GetActiveMemberTravelerIdsAsync(GroupId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _groups.Setup(g => g.FindOverlappingTravelersAsync(Tenant, It.IsAny<IReadOnlyList<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), GroupId,
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    [Fact]
    public async Task Register_quotes_package_price_by_room_type_and_category()
    {
        GivenGroup("Open");
        GivenCustomerWithTravelers(101, 102);
        _groups.Setup(g => g.RegisterMembersAsync(Tenant, GroupId, It.IsAny<IReadOnlyList<TravelGroupNewMember>>(), 7, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await RegisterHandler().Handle(new RegisterTravelGroupMembersCommand(GroupId, new RegisterTravelGroupMembersDto(30,
            [new RegisterTravelGroupMemberItem(101, "Quad", "Adult"), new RegisterTravelGroupMemberItem(102, "Quad", "Child")])), CancellationToken.None);

        result.Data.Should().Be(2);
        _groups.Verify(g => g.RegisterMembersAsync(Tenant, GroupId, It.Is<IReadOnlyList<TravelGroupNewMember>>(m =>
            m[0].QuotedPrice == 300_000 && m[1].QuotedPrice == 250_000 && m.All(x => x.CurrencyCode == "PKR" && x.CustomerId == 30)),
            7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_refuses_closed_group_other_customers_travelers_overlaps_and_unpriced_rooms()
    {
        GivenGroup("RegistrationClosed");
        GivenCustomerWithTravelers(101);
        var one = new RegisterTravelGroupMembersDto(30, [new RegisterTravelGroupMemberItem(101, "Quad", "Adult")]);
        (await RegisterHandler().Handle(new RegisterTravelGroupMembersCommand(GroupId, one), CancellationToken.None)).Success.Should().BeFalse();

        GivenGroup("Open");
        var stranger = new RegisterTravelGroupMembersDto(30, [new RegisterTravelGroupMemberItem(999, "Quad", "Adult")]);
        (await RegisterHandler().Handle(new RegisterTravelGroupMembersCommand(GroupId, stranger), CancellationToken.None)).Message.Should().Contain("selected customer");

        var single = new RegisterTravelGroupMembersDto(30, [new RegisterTravelGroupMemberItem(101, "Single", "Adult")]);
        (await RegisterHandler().Handle(new RegisterTravelGroupMembersCommand(GroupId, single), CancellationToken.None)).Message.Should().Contain("no Single price");

        _groups.Setup(g => g.FindOverlappingTravelersAsync(Tenant, It.IsAny<IReadOnlyList<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), GroupId,
            It.IsAny<CancellationToken>())).ReturnsAsync([101]);
        (await RegisterHandler().Handle(new RegisterTravelGroupMembersCommand(GroupId, one), CancellationToken.None)).Message.Should().Contain("overlapping");
        _groups.Verify(g => g.RegisterMembersAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<TravelGroupNewMember>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_when_full_raises_conflict()
    {
        GivenGroup("Open");
        GivenCustomerWithTravelers(101);
        _groups.Setup(g => g.RegisterMembersAsync(Tenant, GroupId, It.IsAny<IReadOnlyList<TravelGroupNewMember>>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        var act = () => RegisterHandler().Handle(new RegisterTravelGroupMembersCommand(GroupId,
            new RegisterTravelGroupMembersDto(30, [new RegisterTravelGroupMemberItem(101, "Quad", "Adult")])), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    // ── Transitions ────────────────────────────────────────────────────────

    [Fact]
    public async Task Visa_processing_requires_every_pilgrim_confirmed_and_opens_the_group_case()
    {
        GivenGroup("RegistrationClosed");
        GivenMembers(new TravelGroupMemberFacts(1, 101, 30, "Male", "Adult", "Quad", null, "Registered"));
        (await TransitionHandler().Handle(new TransitionTravelGroupCommand(GroupId, new TransitionTravelGroupDto(A.StartVisaProcessing, null)), CancellationToken.None))
            .Message.Should().Contain("Confirm or cancel");

        GivenMembers(new TravelGroupMemberFacts(1, 101, 30, "Male", "Adult", "Quad", null, "Confirmed"));
        _config.Setup(c => c.GetVisaTypeByIdAsync(3, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaTypeDto { Id = 3, CountryCode = "SA", IsActive = true });
        _groups.Setup(g => g.StartVisaProcessingAsync(Tenant, GroupId, "RegistrationClosed", 3, "SA", new DateTime(2027, 1, 10), 7,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(88);

        var ok = await TransitionHandler().Handle(new TransitionTravelGroupCommand(GroupId, new TransitionTravelGroupDto(A.StartVisaProcessing, null)), CancellationToken.None);
        ok.Data.Should().Be("VisaProcessing");
    }

    [Fact]
    public async Task Mark_ready_explains_what_is_missing()
    {
        GivenGroup("VisaProcessing", visaCaseId: 88);
        GivenMembers(new TravelGroupMemberFacts(1, 101, 30, "Male", "Adult", "Quad", null, "Confirmed"));
        _visa.Setup(v => v.GetStateAsync(88, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaCaseState(88, null, "Approved", null, null, "SA", 3, null, null, GroupId));
        _groups.Setup(g => g.GetMembersAsync(GroupId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelGroupMemberDto { Id = 1, Status = "Confirmed", VisaDecision = "Approved" }]);
        _groups.Setup(g => g.GetStaysAsync(GroupId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([new TravelGroupStayDto { Id = 11 }]);
        _groups.Setup(g => g.GetRoomsAsync(GroupId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await TransitionHandler().Handle(new TransitionTravelGroupCommand(GroupId, new TransitionTravelGroupDto(A.MarkReady, null)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no room");
        _groups.Verify(g => g.ChangeStatusAsync(It.IsAny<int>(), It.IsAny<TravelGroupStatusChange>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Group_of_other_tenant_or_branch_is_not_found()
    {
        _groups.Setup(g => g.GetStateAsync(GroupId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>())).ReturnsAsync((TravelGroupState?)null);
        var act = () => TransitionHandler().Handle(new TransitionTravelGroupCommand(GroupId, new TransitionTravelGroupDto(A.Open, null)), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Member cancellation ↔ visa case ────────────────────────────────────

    private CancelTravelGroupMemberCommandHandler CancelHandler()
        => new(_groups.Object, _visa.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    private void GivenGroupVisaCase(string visaStatus, string applicantDecision = "Pending")
    {
        _visa.Setup(v => v.GetStateAsync(88, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaCaseState(88, null, visaStatus, null, null, "SA", 3, null, null, GroupId));
        _visa.Setup(v => v.GetApplicantsAsync(88, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelVisaApplicantDto { Id = 500, TravelerId = 101, Decision = applicantDecision }]);
    }

    [Theory]
    [InlineData("Submitted", false)]
    [InlineData("InProcessing", false)]
    [InlineData("DocumentsPending", true)]
    [InlineData("Draft", true)]
    public async Task Cancelling_a_pilgrim_withdraws_them_from_the_group_visa_case_in_the_same_write(string visaStatus, bool expectRemoval)
    {
        GivenGroup("VisaProcessing", visaCaseId: 88);
        GivenMembers(new TravelGroupMemberFacts(1, 101, 30, "Male", "Adult", "Quad", null, "Confirmed"));
        GivenGroupVisaCase(visaStatus);
        TravelVisaWithdrawal? passed = null;
        _groups.Setup(g => g.CancelMemberAsync(Tenant, GroupId, 1, "Medical", 7, It.IsAny<TravelVisaWithdrawal?>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, int, string, int?, TravelVisaWithdrawal?, CancellationToken>((_, _, _, _, _, w, _) => passed = w)
            .ReturnsAsync(true);

        var result = await CancelHandler().Handle(new CancelTravelGroupMemberCommand(GroupId, 1, new CancelTravelGroupMemberDto("Medical")), CancellationToken.None);

        result.Message.Should().Contain("Removed from the group visa case");
        // The repository applies the withdrawal inside the cancellation's transaction; the handler never calls the visa repository to write.
        passed.Should().Be(new TravelVisaWithdrawal(88, 500, visaStatus, expectRemoval, "Pilgrim cancelled: Medical"));
        _visa.Verify(v => v.RecordDecisionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<RecordTravelVisaDecisionDto>(),
            It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _visa.Verify(v => v.RemoveApplicantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Cancelled", "Pending")]
    [InlineData("Approved", "Pending")]
    [InlineData("Submitted", "Withdrawn")]
    public async Task A_decided_case_or_an_already_withdrawn_applicant_needs_no_visa_step(string visaStatus, string decision)
    {
        GivenGroup("VisaProcessing", visaCaseId: 88);
        GivenMembers(new TravelGroupMemberFacts(1, 101, 30, "Male", "Adult", "Quad", null, "Confirmed"));
        GivenGroupVisaCase(visaStatus, decision);
        _groups.Setup(g => g.CancelMemberAsync(Tenant, GroupId, 1, "Medical", 7, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CancelHandler().Handle(new CancelTravelGroupMemberCommand(GroupId, 1, new CancelTravelGroupMemberDto("Medical")), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().NotContain("visa case");
        _groups.Verify(g => g.CancelMemberAsync(Tenant, GroupId, 1, "Medical", 7, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_visa_case_that_moved_meanwhile_rolls_the_cancellation_back_and_asks_to_reload()
    {
        GivenGroup("VisaProcessing", visaCaseId: 88);
        GivenMembers(new TravelGroupMemberFacts(1, 101, 30, "Male", "Adult", "Quad", null, "Confirmed"));
        GivenGroupVisaCase("Submitted");
        _groups.Setup(g => g.CancelMemberAsync(Tenant, GroupId, 1, "Medical", 7, It.IsAny<TravelVisaWithdrawal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // the repository found the case in another status and rolled everything back

        var act = () => CancelHandler().Handle(new CancelTravelGroupMemberCommand(GroupId, 1, new CancelTravelGroupMemberDto("Medical")), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("nothing was cancelled");
    }

    // ── Rooming & transport ────────────────────────────────────────────────

    [Fact]
    public async Task Rooming_is_validated_before_saving()
    {
        GivenGroup("VisaProcessing");
        GivenMembers(new TravelGroupMemberFacts(1, 101, 30, "Male", "Adult", "Quad", null, "Confirmed"));
        _groups.Setup(g => g.GetStaysAsync(GroupId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([new TravelGroupStayDto { Id = 11 }]);
        var handler = new SaveTravelGroupRoomingCommandHandler(_groups.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new SaveTravelGroupRoomingCommand(GroupId, 11,
            new SaveTravelStayRoomingDto([new TravelRoomingRoomDto("1", "Quad", "Female", [1])])), CancellationToken.None);

        result.Message.Should().Contain("women");
        _groups.Verify(g => g.SaveRoomingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<TravelRoomingRoomDto>>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transport_fleet_booking_must_belong_to_the_tenant()
    {
        GivenGroup("Open");
        _groups.Setup(g => g.FleetBookingExistsAsync(Tenant, 4242, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var handler = new SaveTravelGroupTransportCommandHandler(_groups.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new SaveTravelGroupTransportCommand(GroupId, null, new SaveTravelTransportLegDto(
            "AirportTransfer", new DateTime(2027, 1, 10, 9, 0, 0), "JED airport", "Makkah hotel", "Al Haramain", "Coaster", 25, 4242, null)), CancellationToken.None);

        result.Message.Should().Contain("Fleet booking not found");
    }

    // ── Manifest ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Manifest_is_audited_fail_closed_before_data_is_read()
    {
        _groups.Setup(g => g.GetByIdAsync(GroupId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelGroupDto { Id = GroupId, GroupNumber = "GRP-2027-00001" });
        _audit.Setup(a => a.RecordAsync(It.IsAny<AuditEventWrite>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("audit down"));
        var handler = new GetTravelGroupManifestQueryHandler(_groups.Object, _travelers.Object, _audit.Object, _scope.Object, _tenant.Object, _user.Object);

        var act = () => handler.Handle(new GetTravelGroupManifestQuery(GroupId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _groups.Verify(g => g.GetMembersAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.RecordAsync(It.Is<AuditEventWrite>(w => w.FailPolicy == AuditFailPolicy.FailClosed && w.EntityId == GroupId), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Packages ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Package_publish_requires_prices_stays_and_visa_type()
    {
        var handler = new ChangeTravelPackageStatusCommandHandler(_packages.Object, _tenant.Object);
        _packages.Setup(p => p.GetByIdAsync(PackageId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Package("Draft", visaTypeId: null));
        (await handler.Handle(new ChangeTravelPackageStatusCommand(PackageId, "Publish"), CancellationToken.None)).Message.Should().Contain("visa type");

        _packages.Setup(p => p.GetByIdAsync(PackageId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Package("Draft"));
        _packages.Setup(p => p.SetStatusAsync(PackageId, Tenant, "Draft", "Published", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await handler.Handle(new ChangeTravelPackageStatusCommand(PackageId, "Publish"), CancellationToken.None)).Data.Should().Be("Published");
    }

    [Fact]
    public void Package_validator_rejects_duplicate_room_prices_and_nights_overflow()
    {
        var dto = new SaveTravelPackageDto("UMR-14", "Umrah 14", "Umrah", 14, 10, 6, 3, null, null, "PKR",
            [new SaveTravelPackageStayDto(1, 10)],
            [new SaveTravelPackagePriceDto("Quad", 1, 1, 1), new SaveTravelPackagePriceDto("Quad", 2, 2, 2)]);
        var errors = new SaveTravelPackageCommandValidator().Validate(new SaveTravelPackageCommand(null, dto)).Errors.Select(e => e.ErrorMessage).ToList();
        errors.Should().Contain(m => m.Contains("priced once"));
        errors.Should().Contain(m => m.Contains("cannot exceed the package duration"));
    }
}
