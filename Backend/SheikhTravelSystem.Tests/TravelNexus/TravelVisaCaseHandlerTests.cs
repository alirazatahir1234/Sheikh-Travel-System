using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;
using A = SheikhTravelSystem.Application.Features.TravelNexus.Common.VisaCaseStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelVisaCaseHandlerTests
{
    private const int Tenant = 8;
    private const int CaseId = 30;
    private const int CustomerId = 12;

    private readonly Mock<ITravelVisaCaseRepository> _cases = new();
    private readonly Mock<ITravelCustomerRepository> _customers = new();
    private readonly Mock<ITravelEnquiryRepository> _enquiries = new();
    private readonly Mock<ITravelTravelerRepository> _travelers = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelVisaCaseHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _user.Setup(u => u.UserId).Returns(2);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
    }

    private TravelVisaCaseGuard Guard => new(_cases.Object);

    private void GivenCase(string status, string? onHoldFrom = null, int applicants = 1)
    {
        _cases.Setup(c => c.GetStateAsync(CaseId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaCaseState(CaseId, CustomerId, status, onHoldFrom, null));
        _cases.Setup(c => c.GetApplicantsAsync(CaseId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, applicants).Select(i => new TravelVisaApplicantDto { Id = i, Decision = "Pending" }).ToList());
    }

    private TransitionTravelVisaCaseCommandHandler TransitionHandler() => new(_cases.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_takes_country_from_visa_type_and_branch_from_customer()
    {
        _customers.Setup(c => c.GetRefAsync(CustomerId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelCustomerRef(CustomerId, 5));
        _config.Setup(c => c.GetVisaTypeByIdAsync(3, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaTypeDto { Id = 3, CountryCode = "SA", IsActive = true });
        _cases.Setup(c => c.CreateAsync(Tenant, It.IsAny<CreateTravelVisaCaseDto>(), "SA", 5, 2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CaseId);
        var handler = new CreateTravelVisaCaseCommandHandler(_cases.Object, _customers.Object, _enquiries.Object, _config.Object,
            _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new CreateTravelVisaCaseCommand(new CreateTravelVisaCaseDto(
            CustomerId, 3, null, null, null, null, "Normal", null, 15000m, 5000m, "PKR", null)), CancellationToken.None);

        result.Data.Should().Be(CaseId);
    }

    [Fact]
    public async Task Create_for_customer_of_another_tenant_is_not_found()
    {
        _customers.Setup(c => c.GetRefAsync(CustomerId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync((TravelCustomerRef?)null);
        var handler = new CreateTravelVisaCaseCommandHandler(_cases.Object, _customers.Object, _enquiries.Object, _config.Object,
            _scope.Object, _tenant.Object, _user.Object);

        var act = () => handler.Handle(new CreateTravelVisaCaseCommand(new CreateTravelVisaCaseDto(
            CustomerId, 3, null, null, null, null, "Normal", null, null, null, null, null)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void Fees_require_currency()
        => new CreateTravelVisaCaseCommandValidator().Validate(new CreateTravelVisaCaseCommand(new CreateTravelVisaCaseDto(
            CustomerId, 3, null, null, null, null, "Normal", null, 15000m, null, null, null))).IsValid.Should().BeFalse();

    // ── Transitions ────────────────────────────────────────────────────────

    [Fact]
    public async Task Transition_not_allowed_is_refused_without_writing()
    {
        GivenCase("Draft");
        var result = await TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId,
            new TransitionTravelVisaCaseDto(A.StartProcessing, null)), CancellationToken.None);

        result.Success.Should().BeFalse();
        _cases.Verify(c => c.ChangeStatusAsync(It.IsAny<int>(), It.IsAny<TravelVisaCaseStatusChange>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_documents_needs_an_applicant()
    {
        GivenCase("DocumentsPending", applicants: 0);
        var result = await TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId,
            new TransitionTravelVisaCaseDto(A.VerifyDocuments, null)), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Hold_remembers_status_and_resume_returns_to_it()
    {
        GivenCase("Submitted");
        _cases.Setup(c => c.ChangeStatusAsync(Tenant, It.IsAny<TravelVisaCaseStatusChange>(), 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.Hold, "Awaiting bank letter")), CancellationToken.None);
        _cases.Verify(c => c.ChangeStatusAsync(Tenant, It.Is<TravelVisaCaseStatusChange>(x =>
            x.ExpectedStatus == "Submitted" && x.NewStatus == "OnHold" && x.NewOnHoldFromStatus == "Submitted"), 2, It.IsAny<CancellationToken>()), Times.Once);

        GivenCase("OnHold", onHoldFrom: "Submitted");
        await TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.Resume, null)), CancellationToken.None);
        _cases.Verify(c => c.ChangeStatusAsync(Tenant, It.Is<TravelVisaCaseStatusChange>(x =>
            x.ExpectedStatus == "OnHold" && x.NewStatus == "Submitted" && x.NewOnHoldFromStatus == null), 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Concurrent_transition_raises_conflict()
    {
        GivenCase("Draft");
        _cases.Setup(c => c.ChangeStatusAsync(Tenant, It.IsAny<TravelVisaCaseStatusChange>(), 2, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.StartDocuments, null)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public void Cancel_requires_reason()
        => new TransitionTravelVisaCaseCommandValidator()
            .Validate(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.Cancel, null)))
            .IsValid.Should().BeFalse();

    [Fact]
    public void Generic_transition_cannot_submit_or_set_an_outcome()
    {
        var v = new TransitionTravelVisaCaseCommandValidator();
        v.Validate(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.Submit, null))).IsValid.Should().BeFalse();
        v.Validate(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto("Approve", null))).IsValid.Should().BeFalse();
    }

    // ── Submit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_requires_verified_documents()
    {
        GivenCase("DocumentsPending");
        var handler = new SubmitTravelVisaCaseCommandHandler(_cases.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new SubmitTravelVisaCaseCommand(CaseId,
            new SubmitTravelVisaCaseDto(DateTime.UtcNow, "GWF123", null, null)), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Submit_writes_date_and_reference()
    {
        GivenCase("DocumentsVerified");
        _cases.Setup(c => c.ChangeStatusAsync(Tenant, It.IsAny<TravelVisaCaseStatusChange>(), 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new SubmitTravelVisaCaseCommandHandler(_cases.Object, Guard, _scope.Object, _tenant.Object, _user.Object);
        var at = DateTime.UtcNow.AddHours(-1);

        await handler.Handle(new SubmitTravelVisaCaseCommand(CaseId, new SubmitTravelVisaCaseDto(at, " GWF123 ", null, null)), CancellationToken.None);

        _cases.Verify(c => c.ChangeStatusAsync(Tenant, It.Is<TravelVisaCaseStatusChange>(x =>
            x.NewStatus == "Submitted" && x.SubmittedAt == at && x.SubmissionReference == "GWF123"), 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Submission_date_cannot_be_in_the_future()
        => new SubmitTravelVisaCaseCommandValidator()
            .Validate(new SubmitTravelVisaCaseCommand(CaseId, new SubmitTravelVisaCaseDto(DateTime.UtcNow.AddDays(2), null, null, null)))
            .IsValid.Should().BeFalse();

    // ── Applicants ─────────────────────────────────────────────────────────

    private AddTravelVisaApplicantsCommandHandler AddHandler() => new(_cases.Object, _travelers.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    [Fact]
    public async Task Applicants_must_be_travelers_of_the_same_customer()
    {
        GivenCase("Draft");
        _travelers.Setup(t => t.GetByCustomerAsync(CustomerId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelTravelerDto { Id = 1 }, new TravelTravelerDto { Id = 2 }]);

        var result = await AddHandler().Handle(new AddTravelVisaApplicantsCommand(CaseId, new AddTravelVisaApplicantsDto([1, 99])), CancellationToken.None);

        result.Success.Should().BeFalse();
        _cases.Verify(c => c.AddApplicantsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Applicants_cannot_be_added_after_submission()
    {
        GivenCase("Submitted");
        var result = await AddHandler().Handle(new AddTravelVisaApplicantsCommand(CaseId, new AddTravelVisaApplicantsDto([1])), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Applicants_are_added_with_expected_status()
    {
        GivenCase("DocumentsPending");
        _travelers.Setup(t => t.GetByCustomerAsync(CustomerId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelTravelerDto { Id = 1 }, new TravelTravelerDto { Id = 2 }]);
        _cases.Setup(c => c.AddApplicantsAsync(Tenant, CaseId, "DocumentsPending", It.IsAny<IReadOnlyList<int>>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await AddHandler().Handle(new AddTravelVisaApplicantsCommand(CaseId, new AddTravelVisaApplicantsDto([1, 2, 2])), CancellationToken.None);

        result.Data.Should().Be(2);
        _cases.Verify(c => c.AddApplicantsAsync(Tenant, CaseId, "DocumentsPending",
            It.Is<IReadOnlyList<int>>(ids => ids.Count == 2), 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Decisions ──────────────────────────────────────────────────────────

    private RecordTravelVisaDecisionCommandHandler DecisionHandler() => new(_cases.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    [Fact]
    public async Task Decisions_only_after_submission()
    {
        GivenCase("DocumentsVerified");
        var result = await DecisionHandler().Handle(new RecordTravelVisaDecisionCommand(CaseId, 1,
            new RecordTravelVisaDecisionDto("Approved", null, null, null, null, null, null)), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Approval_keeps_visa_fields_and_drops_rejection_reason()
    {
        GivenCase("InProcessing");
        _cases.Setup(c => c.RecordDecisionAsync(Tenant, CaseId, 1, "InProcessing", It.IsAny<RecordTravelVisaDecisionDto>(), 2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaDecisionResult(true, "Approved"));

        var result = await DecisionHandler().Handle(new RecordTravelVisaDecisionCommand(CaseId, 1, new RecordTravelVisaDecisionDto(
            "Approved", " e123 ", new DateTime(2026, 11, 20), new DateTime(2026, 12, 1), new DateTime(2027, 2, 28), "ignored", null)), CancellationToken.None);

        result.Data!.NewCaseStatus.Should().Be("Approved");
        _cases.Verify(c => c.RecordDecisionAsync(Tenant, CaseId, 1, "InProcessing", It.Is<RecordTravelVisaDecisionDto>(d =>
            d.VisaNumber == "E123" && d.RejectionReason == null && d.VisaExpiryDate == new DateTime(2027, 2, 28)),
            2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Decision_on_missing_applicant_or_changed_case_raises_conflict()
    {
        GivenCase("Submitted");
        _cases.Setup(c => c.RecordDecisionAsync(Tenant, CaseId, 9, "Submitted", It.IsAny<RecordTravelVisaDecisionDto>(), 2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaDecisionResult(false, null));

        var act = () => DecisionHandler().Handle(new RecordTravelVisaDecisionCommand(CaseId, 9,
            new RecordTravelVisaDecisionDto("Withdrawn", null, null, null, null, null, null)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public void Rejection_requires_reason_and_pending_is_not_recordable()
    {
        var v = new RecordTravelVisaDecisionCommandValidator();
        v.Validate(new RecordTravelVisaDecisionCommand(CaseId, 1, new RecordTravelVisaDecisionDto("Rejected", null, null, null, null, null, null))).IsValid.Should().BeFalse();
        v.Validate(new RecordTravelVisaDecisionCommand(CaseId, 1, new RecordTravelVisaDecisionDto("Pending", null, null, null, null, null, null))).IsValid.Should().BeFalse();
    }

    // ── Queries / isolation ────────────────────────────────────────────────

    [Fact]
    public async Task Case_of_other_tenant_or_branch_is_not_found_for_every_command()
    {
        _cases.Setup(c => c.GetStateAsync(CaseId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TravelVisaCaseState?)null);

        await ((Func<Task>)(() => TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.Hold, null)), CancellationToken.None)))
            .Should().ThrowAsync<NotFoundException>();
        await ((Func<Task>)(() => AddHandler().Handle(new AddTravelVisaApplicantsCommand(CaseId, new AddTravelVisaApplicantsDto([1])), CancellationToken.None)))
            .Should().ThrowAsync<NotFoundException>();
        await ((Func<Task>)(() => DecisionHandler().Handle(new RecordTravelVisaDecisionCommand(CaseId, 1,
            new RecordTravelVisaDecisionDto("Approved", null, null, null, null, null, null)), CancellationToken.None)))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Detail_masks_passports_and_flags_short_validity()
    {
        _cases.Setup(c => c.GetByIdAsync(CaseId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaCaseDto { Id = CaseId, Status = "Draft", IntendedTravelDate = new DateTime(2027, 1, 10) });
        _cases.Setup(c => c.GetApplicantsAsync(CaseId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new TravelVisaApplicantDto { Id = 1, PassportNumber = "AB1234567", PassportExpiryDate = new DateTime(2027, 3, 1) },
            new TravelVisaApplicantDto { Id = 2, PassportNumber = "CD7654321", PassportExpiryDate = new DateTime(2030, 1, 1) }
        ]);
        _cases.Setup(c => c.GetHistoryAsync(CaseId, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _user.Setup(u => u.HasPermission(TravelPermissions.TravelerViewPii)).Returns(false);
        var handler = new GetTravelVisaCaseByIdQueryHandler(_cases.Object, _scope.Object, _tenant.Object, _user.Object);

        var dto = (await handler.Handle(new GetTravelVisaCaseByIdQuery(CaseId), CancellationToken.None)).Data!;

        dto.Applicants[0].PassportWarning.Should().BeTrue();
        dto.Applicants[1].PassportWarning.Should().BeFalse();
        dto.Applicants.Should().OnlyContain(a => a.PassportNumber!.StartsWith(TravelPii.MaskChar));
        dto.AllowedActions.Should().Contain([A.StartDocuments, A.ManageApplicants, A.Cancel]);
    }

    [Fact]
    public void Summary_counts_cover_every_status_and_sum_to_total()
    {
        var summary = GetTravelVisaCaseSummaryQueryHandler.Build(new Dictionary<string, int> { ["Submitted"] = 3, ["Approved"] = 5 });
        summary.Counts.Keys.Should().BeEquivalentTo(TravelValues.VisaCaseStatuses);
        summary.Counts.Values.Sum().Should().Be(summary.Total);
        summary.Total.Should().Be(8);
    }

    [Theory]
    [InlineData(null, "2030-01-01", true)]               // no passport
    [InlineData("AB1234567", "2027-07-09", true)]        // < 6 months after travel
    [InlineData("AB1234567", "2027-07-10", false)]       // exactly 6 months
    public void Passport_warning_uses_six_months_after_travel(string? passport, string expiry, bool expected)
        => GetTravelVisaCaseByIdQueryHandler.NeedsPassportWarning(passport, DateTime.Parse(expiry), new DateTime(2027, 1, 10), DateTime.UtcNow)
            .Should().Be(expected);
}
