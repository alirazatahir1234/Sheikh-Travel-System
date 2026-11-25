using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;
using A = SheikhTravelSystem.Application.Features.TravelNexus.Common.VisaCaseStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelDocumentHandlerTests
{
    private const int Tenant = 6;
    private const int CaseId = 50;
    private const int CustomerId = 20;
    private const int RequirementId = 700;

    private readonly Mock<ITravelDocumentRepository> _documents = new();
    private readonly Mock<ITravelVisaCaseRepository> _cases = new();
    private readonly Mock<ITravelChecklistRepository> _checklists = new();
    private readonly Mock<ITravelTravelerRepository> _travelers = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelDocumentStorage> _storage = new();
    private readonly Mock<IAuditEngine> _audit = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8.ToArray(), 9, 9];

    public TravelDocumentHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _user.Setup(u => u.UserId).Returns(4);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
    }

    private TravelDocumentGuard Guard => new(_documents.Object, _cases.Object);

    private void GivenCase(string status, DateTime? travel = null, int? checklistId = 11)
        => _cases.Setup(c => c.GetStateAsync(CaseId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelVisaCaseState(CaseId, CustomerId, status, null, null, "SA", 3, travel, checklistId));

    private void GivenRequirement(string status, int? minValidity = null, int? checklistItemId = 1)
        => _documents.Setup(d => d.GetRequirementAsync(RequirementId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelRequirementFacts(RequirementId, CaseId, 1, 100, "Passport", true, minValidity, status, checklistItemId, null));

    // ── Upload ─────────────────────────────────────────────────────────────

    private UploadTravelDocumentCommandHandler UploadHandler() => new(_documents.Object, _storage.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

    [Fact]
    public async Task Upload_stores_privately_and_records_file()
    {
        GivenCase("DocumentsPending");
        GivenRequirement("Missing");
        _storage.Setup(s => s.SaveAsync(Pdf, "application/pdf", ".pdf", Tenant, CaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("tenant-6/case-50/k.pdf");
        _documents.Setup(d => d.AddFileAsync(Tenant, CaseId, RequirementId, "DocumentsPending", It.IsAny<TravelNewDocumentFile>(), 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(9);

        var result = await UploadHandler().Handle(new UploadTravelDocumentCommand(RequirementId, "C:\\docs\\passport.pdf", Pdf), CancellationToken.None);

        result.Data.Should().Be(9);
        _documents.Verify(d => d.AddFileAsync(Tenant, CaseId, RequirementId, "DocumentsPending",
            It.Is<TravelNewDocumentFile>(f => f.ContentType == "application/pdf" && f.Sha256.Length == 64 && f.StorageKey == "tenant-6/case-50/k.pdf"),
            4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_with_forged_content_is_refused_before_storage()
    {
        GivenCase("DocumentsPending");
        GivenRequirement("Missing");
        var result = await UploadHandler().Handle(new UploadTravelDocumentCommand(RequirementId, "passport.pdf", [0x4D, 0x5A, 0, 0, 0, 0]), CancellationToken.None);

        result.Success.Should().BeFalse();
        _storage.Verify(s => s.SaveAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("DocumentsVerified")]
    [InlineData("Submitted")]
    [InlineData("Draft")]
    public async Task Documents_change_only_while_collecting(string caseStatus)
    {
        GivenCase(caseStatus);
        GivenRequirement("Missing");
        var result = await UploadHandler().Handle(new UploadTravelDocumentCommand(RequirementId, "passport.pdf", Pdf), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Upload_cleans_up_stored_file_when_case_changed_concurrently()
    {
        GivenCase("DocumentsPending");
        GivenRequirement("Missing");
        _storage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), Tenant, CaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("tenant-6/case-50/k.pdf");
        _documents.Setup(d => d.AddFileAsync(Tenant, CaseId, RequirementId, "DocumentsPending", It.IsAny<TravelNewDocumentFile>(), 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var act = () => UploadHandler().Handle(new UploadTravelDocumentCommand(RequirementId, "passport.pdf", Pdf), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _storage.Verify(s => s.DeleteAsync("tenant-6/case-50/k.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Requirement_of_case_in_other_tenant_or_branch_is_not_found()
    {
        GivenRequirement("Missing");
        _cases.Setup(c => c.GetStateAsync(CaseId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TravelVisaCaseState?)null);
        var act = () => UploadHandler().Handle(new UploadTravelDocumentCommand(RequirementId, "passport.pdf", Pdf), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Review ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_enforces_minimum_validity_after_travel()
    {
        GivenCase("DocumentsPending", travel: new DateTime(2027, 1, 10));
        GivenRequirement("Uploaded", minValidity: 180);
        var handler = new VerifyTravelDocumentCommandHandler(_documents.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var tooShort = await handler.Handle(new VerifyTravelDocumentCommand(RequirementId, new VerifyTravelDocumentDto(new DateTime(2027, 3, 1), null)), CancellationToken.None);
        tooShort.Success.Should().BeFalse();

        _documents.Setup(d => d.ReviewAsync(Tenant, CaseId, RequirementId, "DocumentsPending", It.IsAny<IReadOnlyList<string>>(), "Verified",
            null, new DateTime(2028, 1, 1), 4, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var ok = await handler.Handle(new VerifyTravelDocumentCommand(RequirementId, new VerifyTravelDocumentDto(new DateTime(2028, 1, 1), null)), CancellationToken.None);
        ok.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Missing_document_cannot_be_verified()
    {
        GivenCase("DocumentsPending");
        GivenRequirement("Missing");
        var handler = new VerifyTravelDocumentCommandHandler(_documents.Object, Guard, _scope.Object, _tenant.Object, _user.Object);
        (await handler.Handle(new VerifyTravelDocumentCommand(RequirementId, new VerifyTravelDocumentDto(null, null)), CancellationToken.None))
            .Success.Should().BeFalse();
    }

    [Fact]
    public void Waive_and_reject_require_a_reason()
    {
        new WaiveTravelDocumentCommandValidator().Validate(new WaiveTravelDocumentCommand(RequirementId, new ReviewReasonDto(" "))).IsValid.Should().BeFalse();
        new RejectTravelDocumentCommandValidator().Validate(new RejectTravelDocumentCommand(RequirementId, new ReviewReasonDto(""))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Waive_records_reason_from_allowed_statuses()
    {
        GivenCase("DocumentsPending");
        GivenRequirement("Missing");
        _documents.Setup(d => d.ReviewAsync(Tenant, CaseId, RequirementId, "DocumentsPending", It.IsAny<IReadOnlyList<string>>(), "Waived",
            "Embassy exempts minors", null, 4, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new WaiveTravelDocumentCommandHandler(_documents.Object, Guard, _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new WaiveTravelDocumentCommand(RequirementId, new ReviewReasonDto(" Embassy exempts minors ")), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Checklist_requirements_cannot_be_removed()
    {
        GivenCase("DocumentsPending");
        GivenRequirement("Missing", checklistItemId: 5);
        var handler = new RemoveTravelDocumentRequirementCommandHandler(_documents.Object, Guard, _scope.Object, _tenant.Object, _user.Object);
        (await handler.Handle(new RemoveTravelDocumentRequirementCommand(RequirementId), CancellationToken.None)).Message.Should().Contain("Waive");
    }

    // ── Download ───────────────────────────────────────────────────────────

    private DownloadTravelDocumentFileQueryHandler DownloadHandler()
        => new(_documents.Object, _cases.Object, _storage.Object, _audit.Object, _scope.Object, _tenant.Object);

    [Fact]
    public async Task Download_audits_fail_closed_before_opening_the_file()
    {
        GivenCase("DocumentsPending");
        _documents.Setup(d => d.GetFileAsync(9, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelDocumentFileFacts(9, CaseId, "tenant-6/case-50/k.pdf", "passport.pdf", "application/pdf"));
        var order = new List<string>();
        _audit.Setup(a => a.RecordAsync(It.IsAny<AuditEventWrite>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("audit")).Returns(Task.CompletedTask);
        _storage.Setup(s => s.OpenReadAsync("tenant-6/case-50/k.pdf", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("open")).ReturnsAsync(new MemoryStream([1]));

        var file = await DownloadHandler().Handle(new DownloadTravelDocumentFileQuery(9), CancellationToken.None);

        file.ContentType.Should().Be("application/pdf");
        order.Should().Equal("audit", "open");
        _audit.Verify(a => a.RecordAsync(It.Is<AuditEventWrite>(w => w.FailPolicy == AuditFailPolicy.FailClosed && w.EntityId == 9 && w.Action == "Download"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Download_of_file_in_other_tenant_or_branch_is_not_found_and_not_opened()
    {
        _documents.Setup(d => d.GetFileAsync(9, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelDocumentFileFacts(9, CaseId, "tenant-6/case-50/k.pdf", "passport.pdf", "application/pdf"));
        _cases.Setup(c => c.GetStateAsync(CaseId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TravelVisaCaseState?)null);

        var act = () => DownloadHandler().Handle(new DownloadTravelDocumentFileQuery(9), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _storage.Verify(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.RecordAsync(It.IsAny<AuditEventWrite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Download_is_refused_when_audit_cannot_be_written()
    {
        GivenCase("DocumentsPending");
        _documents.Setup(d => d.GetFileAsync(9, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelDocumentFileFacts(9, CaseId, "k", "passport.pdf", "application/pdf"));
        _audit.Setup(a => a.RecordAsync(It.IsAny<AuditEventWrite>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("audit down"));

        var act = () => DownloadHandler().Handle(new DownloadTravelDocumentFileQuery(9), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _storage.Verify(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Visa case gate & snapshot ──────────────────────────────────────────

    private TransitionTravelVisaCaseCommandHandler TransitionHandler()
        => new(_cases.Object, _documents.Object, new TravelVisaCaseGuard(_cases.Object), new TravelDocumentPlanner(_checklists.Object, _travelers.Object),
            _scope.Object, _tenant.Object, _user.Object);

    private void GivenApplicants(params (int Id, int TravelerId)[] applicants)
        => _cases.Setup(c => c.GetApplicantsAsync(CaseId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(applicants.Select(a => new TravelVisaApplicantDto { Id = a.Id, TravelerId = a.TravelerId, Decision = "Pending" }).ToList());

    [Fact]
    public async Task Verify_documents_is_blocked_until_mandatory_documents_are_complete()
    {
        GivenCase("DocumentsPending");
        GivenApplicants((1, 101));
        _documents.Setup(d => d.GetRequirementStatesAsync(CaseId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelRequirementState(1, true, "Verified"), new TravelRequirementState(1, true, "Rejected")]);

        var result = await TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.VerifyDocuments, null)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("1 of 2");
        _cases.Verify(c => c.ChangeStatusAsync(It.IsAny<int>(), It.IsAny<TravelVisaCaseStatusChange>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_documents_succeeds_when_all_mandatory_are_verified_or_waived()
    {
        GivenCase("DocumentsPending");
        GivenApplicants((1, 101));
        _documents.Setup(d => d.GetRequirementStatesAsync(CaseId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelRequirementState(1, true, "Verified"), new TravelRequirementState(1, true, "Waived")]);
        _cases.Setup(c => c.ChangeStatusAsync(Tenant, It.IsAny<TravelVisaCaseStatusChange>(), 4, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.VerifyDocuments, null)), CancellationToken.None);

        result.Data.Should().Be("DocumentsVerified");
    }

    [Fact]
    public async Task Start_documents_snapshots_the_active_checklist_in_the_status_change()
    {
        GivenCase("Draft", travel: new DateTime(2027, 1, 10), checklistId: null);
        GivenApplicants((1, 101), (2, 102));
        _checklists.Setup(c => c.GetActiveForAsync(Tenant, "SA", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelChecklistSnapshotSource(11, 2,
            [
                new TravelChecklistItemDto { Id = 1, DocumentTypeId = 100, IsMandatory = true, ApplicantCondition = "All" },
                new TravelChecklistItemDto { Id = 2, DocumentTypeId = 200, IsMandatory = true, ApplicantCondition = "Minor" }
            ]));
        _travelers.Setup(t => t.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<int> ids, int _, CancellationToken _) => new List<TravelTravelerDto>
            {
                new() { Id = 101, Gender = "Male", DateOfBirth = new DateTime(1980, 1, 1) },
                new() { Id = 102, Gender = "Female", DateOfBirth = new DateTime(2015, 1, 1) },
                new() { Id = 103, Gender = "Male" } // not an applicant: filtered out because it is not requested
            }.Where(t => ids.Contains(t.Id)).ToList());
        _cases.Setup(c => c.ChangeStatusAsync(Tenant, It.IsAny<TravelVisaCaseStatusChange>(), 4, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await TransitionHandler().Handle(new TransitionTravelVisaCaseCommand(CaseId, new TransitionTravelVisaCaseDto(A.StartDocuments, null)), CancellationToken.None);

        _cases.Verify(c => c.ChangeStatusAsync(Tenant, It.Is<TravelVisaCaseStatusChange>(x =>
            x.NewStatus == "DocumentsPending" && x.ChecklistTemplateId == 11 && x.ChecklistVersion == 2 &&
            x.RequirementSeeds!.Count == 3 &&
            x.RequirementSeeds.Count(s => s.TravelerId == 102) == 2 &&
            x.RequirementSeeds.All(s => s.TravelerId != 103)), 4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task New_mandatory_applicant_sends_verified_case_back_to_pending()
    {
        GivenCase("DocumentsVerified", checklistId: 11);
        GivenApplicants((1, 101));
        _checklists.Setup(c => c.GetItemsAsync(11, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelChecklistItemDto { Id = 1, DocumentTypeId = 100, IsMandatory = true, ApplicantCondition = "All" }]);
        _travelers.Setup(t => t.GetByCustomerAsync(CustomerId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelTravelerDto { Id = 101, Gender = "Male" }, new TravelTravelerDto { Id = 102, Gender = "Male" }]);
        _travelers.Setup(t => t.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelTravelerDto { Id = 102, Gender = "Male" }]);
        _cases.Setup(c => c.AddApplicantsAsync(Tenant, CaseId, "DocumentsVerified", It.IsAny<IReadOnlyList<int>>(), 4,
            It.IsAny<IReadOnlyList<TravelRequirementSeed>>(), true, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var handler = new AddTravelVisaApplicantsCommandHandler(_cases.Object, _travelers.Object, new Mock<ITravelGroupRepository>().Object, new TravelVisaCaseGuard(_cases.Object),
            new TravelDocumentPlanner(_checklists.Object, _travelers.Object), _scope.Object, _tenant.Object, _user.Object);

        var result = await handler.Handle(new AddTravelVisaApplicantsCommand(CaseId, new AddTravelVisaApplicantsDto([102])), CancellationToken.None);

        result.Data.Should().Be(1);
        _cases.Verify(c => c.AddApplicantsAsync(Tenant, CaseId, "DocumentsVerified", It.IsAny<IReadOnlyList<int>>(), 4,
            It.Is<IReadOnlyList<TravelRequirementSeed>>(s => s.Count == 1 && s[0].TravelerId == 102), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Checklists ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Empty_or_non_draft_checklists_cannot_be_activated()
    {
        var guard = new TravelChecklistGuard(_checklists.Object, _config.Object);
        var handler = new ActivateTravelChecklistCommandHandler(_checklists.Object, guard, _tenant.Object);

        _checklists.Setup(c => c.GetByIdAsync(1, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelChecklistTemplateDto { Id = 1, Status = "Draft" });
        (await handler.Handle(new ActivateTravelChecklistCommand(1), CancellationToken.None)).Success.Should().BeFalse();

        _checklists.Setup(c => c.GetByIdAsync(2, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelChecklistTemplateDto { Id = 2, Status = "Active", Items = [new TravelChecklistItemDto()] });
        (await handler.Handle(new ActivateTravelChecklistCommand(2), CancellationToken.None)).Success.Should().BeFalse();
        _checklists.Verify(c => c.ActivateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Checklist_visa_type_must_belong_to_its_country()
    {
        _config.Setup(c => c.CountryExistsAsync("AE", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _config.Setup(c => c.GetVisaTypeByIdAsync(3, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelVisaTypeDto { Id = 3, CountryCode = "SA" });
        var guard = new TravelChecklistGuard(_checklists.Object, _config.Object);

        (await guard.ValidateScopeAsync(Tenant, new SaveTravelChecklistTemplateDto("AE", 3, "UAE tourist", null), CancellationToken.None))
            .Should().Contain("different country");
    }

    [Fact]
    public void Checklist_items_cannot_repeat_document_and_condition()
        => new SaveTravelChecklistItemsCommandValidator().Validate(new SaveTravelChecklistItemsCommand(1,
            [new SaveTravelChecklistItemDto(100, true, "All", null, null), new SaveTravelChecklistItemDto(100, false, "All", null, null)]))
            .IsValid.Should().BeFalse();
}
