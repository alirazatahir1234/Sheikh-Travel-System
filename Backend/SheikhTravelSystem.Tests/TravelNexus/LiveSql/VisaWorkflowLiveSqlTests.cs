using System.Text;
using FluentAssertions;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;
using V = SheikhTravelSystem.Application.Features.TravelNexus.Common.VisaCaseStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus.LiveSql;

/// <summary>
/// Brief workflow 1 against a real database: enquiry → customer + visa case → applicant and mandatory document →
/// verification gate → submission → decision, with the dashboard and the visa-processing report reflecting it,
/// and company B unable to see any of it.
/// </summary>
[Collection(TravelNexusLiveSqlCollection.Name)]
public class VisaWorkflowLiveSqlTests(TravelNexusLiveDbFixture fx)
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    /// <summary>Smallest byte sequence the upload validator accepts as a PDF (magic bytes + trailer).</summary>
    internal static byte[] PdfBytes(string marker) => Encoding.ASCII.GetBytes($"%PDF-1.4\n% {marker}\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

    [TravelLiveSqlFact]
    public async Task Enquiry_to_approved_visa_case_end_to_end_and_invisible_to_company_B()
    {
        using var a = fx.Open(fx.A);
        var travelFrom = Today.AddDays(45);

        // ── Setup: visa type and a document type ────────────────────────────
        var visaTypeId = await a.Ok(new SaveTravelVisaTypeCommand(null,
            new SaveTravelVisaTypeDto("SA", "Umrah visa (E2E visa flow)", "Umrah", "Single", 90, 30, 7, 500m, "SAR", true, null)));
        var docTypeId = await a.Ok(new SaveTravelDocumentTypeCommand(null,
            new SaveTravelDocumentTypeDto("PASSPORT-E2E", "Passport copy", null, RequiresExpiryDate: true, IsActive: true)));

        // ── Enquiry → conversion opens the customer and a Draft case ────────
        var enquiryId = await a.Ok(new CreateTravelEnquiryCommand(new SaveTravelEnquiryDto(
            null, "Ahmed Khan", "+92 300 1234567", null, "Visa", "SA", visaTypeId, travelFrom, travelFrom.AddDays(14),
            1, 0, 0, null, null, null, fx.A.UserId, fx.A.BranchId, "Normal", null, null)));
        var conversion = await a.Ok(new ConvertTravelEnquiryCommand(enquiryId,
            new ConvertTravelEnquiryDto(null, "Individual", CreateVisaCase: true, "SA", visaTypeId, travelFrom)));
        conversion.VisaCaseId.Should().NotBeNull();
        var customerId = conversion.CustomerId;
        var caseId = conversion.VisaCaseId!.Value;

        (await a.Ok(new GetTravelEnquiryByIdQuery(enquiryId))).Status.Should().Be("Won");
        (await a.Ok(new GetTravelVisaCaseByIdQuery(caseId))).Status.Should().Be("Draft");

        // ── Traveler becomes the applicant ─────────────────────────────────
        var travelerId = await a.Ok(new CreateTravelTravelerCommand(customerId, new SaveTravelTravelerDto(
            null, "Ahmed", "Khan", "Male", new DateTime(1985, 1, 1), null, "PK", "AB1234567", "PK",
            new DateTime(2020, 1, 1), new DateTime(2030, 1, 1), null, null, null, null, null, null)));
        await a.Ok(new AddTravelVisaApplicantsCommand(caseId, new AddTravelVisaApplicantsDto([travelerId])));
        var applicantId = (await a.Ok(new GetTravelVisaCaseByIdQuery(caseId))).Applicants.Should().ContainSingle().Subject.Id;

        // ── Documents: requested, uploaded, verified — the gate holds until verified ──
        await a.Ok(new TransitionTravelVisaCaseCommand(caseId, new TransitionTravelVisaCaseDto(V.StartDocuments, null)));
        var requirementId = await a.Ok(new RequestTravelDocumentCommand(caseId, new RequestTravelDocumentDto(applicantId, docTypeId, IsMandatory: true, null)));

        var tooEarly = await a.Send(new TransitionTravelVisaCaseCommand(caseId, new TransitionTravelVisaCaseDto(V.VerifyDocuments, null)));
        tooEarly.Success.Should().BeFalse("a mandatory document is still missing");

        var fileId = await a.Ok(new UploadTravelDocumentCommand(requirementId, "passport.pdf", PdfBytes("visa-flow")));
        var duplicate = await a.Send(new UploadTravelDocumentCommand(requirementId, "passport-again.pdf", PdfBytes("visa-flow")));
        duplicate.Success.Should().BeFalse("the same bytes are already uploaded for this requirement");

        var documents = await a.Ok(new GetTravelCaseDocumentsQuery(caseId));
        documents.Requirements.Should().ContainSingle(r => r.Id == requirementId && r.Status == "Uploaded" && r.CurrentFileId == fileId);

        await a.Ok(new VerifyTravelDocumentCommand(requirementId, new VerifyTravelDocumentDto(new DateTime(2030, 1, 1), "Checked against original")));
        await a.Ok(new TransitionTravelVisaCaseCommand(caseId, new TransitionTravelVisaCaseDto(V.VerifyDocuments, null)));

        // ── Submission and decision ─────────────────────────────────────────
        await a.Ok(new SubmitTravelVisaCaseCommand(caseId, new SubmitTravelVisaCaseDto(DateTime.UtcNow.AddHours(-1), "SUB-E2E-1", Today.AddDays(10), null)));
        await a.Ok(new TransitionTravelVisaCaseCommand(caseId, new TransitionTravelVisaCaseDto(V.StartProcessing, null)));
        await a.Ok(new RecordTravelVisaDecisionCommand(caseId, applicantId,
            new RecordTravelVisaDecisionDto("Approved", "V-E2E-001", Today, Today, Today.AddDays(90), null, null)));

        var decided = await a.Ok(new GetTravelVisaCaseByIdQuery(caseId));
        decided.Status.Should().Be("Approved");
        decided.Applicants.Should().ContainSingle().Which.Decision.Should().Be("Approved");

        // ── Dashboard and report read the same rows ─────────────────────────
        var dashboard = await a.Ok(new GetTravelDashboardQuery());
        dashboard.VisaCases.Counts.Values.Sum().Should().Be(dashboard.VisaCases.Total, "pipeline parts must add up");
        dashboard.VisaCases.Counts["Approved"].Should().BeGreaterThanOrEqualTo(1);
        dashboard.Enquiries.Counts["Won"].Should().BeGreaterThanOrEqualTo(1);

        var report = await a.Ok(new GetTravelReportQuery(TravelReportGroup.Operational, "visa-processing", Today.AddDays(-30), Today.AddDays(1), null, null, null, null));
        report.Rows.Should().NotBeEmpty();
        report.Summary.Should().ContainKey("windowFrom");

        var inverted = await a.Ok(new GetTravelReportQuery(TravelReportGroup.Operational, "visa-processing", Today.AddDays(1), Today.AddDays(-30), null, null, null, null));
        inverted.Rows.Should().BeEmpty("an inverted range is an empty window, never unfiltered data");

        // ── Company B sees nothing of company A ─────────────────────────────
        using var b = fx.Open(fx.B);
        await b.Invoking(s => s.Send(new GetTravelVisaCaseByIdQuery(caseId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new GetTravelEnquiryByIdQuery(enquiryId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new DownloadTravelDocumentFileQuery(fileId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new RecordTravelVisaDecisionCommand(caseId, applicantId,
            new RecordTravelVisaDecisionDto("Rejected", null, null, null, null, "cross-tenant", null)))).Should().ThrowAsync<NotFoundException>();

        var bDashboard = await b.Ok(new GetTravelDashboardQuery());
        bDashboard.VisaCases.Total.Should().Be(0);
        bDashboard.Enquiries.Total.Should().Be(0);
        (await b.Ok(new GetTravelReportQuery(TravelReportGroup.Operational, "visa-processing", Today.AddDays(-30), Today.AddDays(1), null, null, null, null)))
            .Rows.Should().BeEmpty();
    }
}
