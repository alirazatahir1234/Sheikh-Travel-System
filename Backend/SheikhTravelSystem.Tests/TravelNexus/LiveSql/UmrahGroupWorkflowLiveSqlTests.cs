using Dapper;
using FluentAssertions;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;
using G = SheikhTravelSystem.Application.Features.TravelNexus.Groups.TravelGroupStateMachine.Actions;
using V = SheikhTravelSystem.Application.Features.TravelNexus.Common.VisaCaseStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus.LiveSql;

/// <summary>
/// Brief workflow 2 against a real database: hotel → published package → group → pilgrims → rooming → group visa case
/// → one pilgrim cancelled after submission, which withdraws the applicant in the same transaction; manifest excludes
/// them; company B cannot read or touch the group.
/// </summary>
[Collection(TravelNexusLiveSqlCollection.Name)]
public class UmrahGroupWorkflowLiveSqlTests(TravelNexusLiveDbFixture fx)
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private static SaveTravelTravelerDto Male(string given, string surname, string passport) => new(
        null, given, surname, "Male", new DateTime(1980, 5, 5), null, "PK", passport, "PK",
        new DateTime(2021, 1, 1), new DateTime(2031, 1, 1), null, null, null, null, null, null);

    [TravelLiveSqlFact]
    public async Task Umrah_group_to_cancelled_pilgrim_is_atomic_with_the_visa_case_and_invisible_to_company_B()
    {
        using var a = fx.Open(fx.A);
        var departure = Today.AddDays(60);

        // ── Catalogue: hotel, visa type, published package ───────────────────
        var hotelId = await a.Ok(new SaveTravelHotelCommand(null, new SaveTravelHotelDto("Hilton Makkah (E2E)", "Makkah", "SA", 5, 300, null, null, true)));
        var visaTypeId = await a.Ok(new SaveTravelVisaTypeCommand(null,
            new SaveTravelVisaTypeDto("SA", "Umrah group visa (E2E group flow)", "Umrah", "Single", 90, 30, 7, 300m, "SAR", true, null)));
        var packageId = await a.Ok(new SaveTravelPackageCommand(null, new SaveTravelPackageDto(
            "UMR-E2E", "Umrah economy (E2E)", "Umrah", 10, 10, 0, visaTypeId, null, null, "PKR",
            [new SaveTravelPackageStayDto(hotelId, 10)], [new SaveTravelPackagePriceDto("Quad", 250000m, 150000m, 50000m)])));
        await a.Ok(new ChangeTravelPackageStatusCommand(packageId, "Publish"));

        // ── Group: stay first, then open registration ───────────────────────
        var groupId = await a.Ok(new CreateTravelGroupCommand(new CreateTravelGroupDto(packageId, "Ramadan group (E2E)", departure, 20, null, null)));
        var stayId = await a.Ok(new SaveTravelGroupStayCommand(groupId, null, new SaveTravelGroupStayDto(hotelId, departure, departure.AddDays(10), null)));
        await a.Ok(new TransitionTravelGroupCommand(groupId, new TransitionTravelGroupDto(G.Open, null)));

        // ── Pilgrims: one customer, two male travelers ──────────────────────
        var customerId = await a.Ok(new CreateTravelCustomerCommand(new CreateTravelCustomerDto(
            null, "Individual", "Bilal Ahmed", "+92 300 7654321", null, null, "Lahore", null, null, null, null, fx.A.UserId, fx.A.BranchId, null, null)));
        var t1 = await a.Ok(new CreateTravelTravelerCommand(customerId, Male("Bilal", "Ahmed", "GRP000001")));
        var t2 = await a.Ok(new CreateTravelTravelerCommand(customerId, Male("Usman", "Ahmed", "GRP000002")));
        await a.Ok(new RegisterTravelGroupMembersCommand(groupId, new RegisterTravelGroupMembersDto(customerId,
            [new RegisterTravelGroupMemberItem(t1, "Quad", "Adult"), new RegisterTravelGroupMemberItem(t2, "Quad", "Adult")])));

        var group = await a.Ok(new GetTravelGroupByIdQuery(groupId));
        group.Members.Should().HaveCount(2);
        group.Members.Should().OnlyContain(m => m.QuotedPrice == 250000m, "the Quad adult price is quoted from the package");
        var m1 = group.Members.Single(m => m.TravelerId == t1);
        var m2 = group.Members.Single(m => m.TravelerId == t2);
        await a.Ok(new ConfirmTravelGroupMemberCommand(groupId, m1.Id));
        await a.Ok(new ConfirmTravelGroupMemberCommand(groupId, m2.Id));

        // ── Rooming and visa processing (the group case gets both applicants) ──
        await a.Ok(new SaveTravelGroupRoomingCommand(groupId, stayId,
            new SaveTravelStayRoomingDto([new TravelRoomingRoomDto("101", "Quad", "Male", [m1.Id, m2.Id])])));
        await a.Ok(new TransitionTravelGroupCommand(groupId, new TransitionTravelGroupDto(G.CloseRegistration, null)));
        await a.Ok(new TransitionTravelGroupCommand(groupId, new TransitionTravelGroupDto(G.StartVisaProcessing, null)));

        group = await a.Ok(new GetTravelGroupByIdQuery(groupId));
        group.Status.Should().Be("VisaProcessing");
        var caseId = group.VisaCaseId.Should().NotBeNull().And.Subject!.Value;
        var visa = await a.Ok(new GetTravelVisaCaseByIdQuery(caseId));
        visa.GroupId.Should().Be(groupId);
        visa.Applicants.Select(x => x.TravelerId).Should().BeEquivalentTo([t1, t2]);

        // Submit the group case so the cancellation must record a Withdrawn decision (not a removal).
        await a.Ok(new TransitionTravelVisaCaseCommand(caseId, new TransitionTravelVisaCaseDto(V.StartDocuments, null)));
        await a.Ok(new TransitionTravelVisaCaseCommand(caseId, new TransitionTravelVisaCaseDto(V.VerifyDocuments, null)));
        await a.Ok(new SubmitTravelVisaCaseCommand(caseId, new SubmitTravelVisaCaseDto(DateTime.UtcNow.AddHours(-1), "GRP-SUB-1", null, null)));

        // ── Cancel one pilgrim: member Cancelled and applicant Withdrawn together ──
        var cancelled = await a.Send(new CancelTravelGroupMemberCommand(groupId, m2.Id, new CancelTravelGroupMemberDto("Medical")));
        cancelled.Success.Should().BeTrue(cancelled.Message);
        cancelled.Message.Should().Contain("Removed from the group visa case");

        group = await a.Ok(new GetTravelGroupByIdQuery(groupId));
        group.Members.Single(m => m.Id == m2.Id).Status.Should().Be("Cancelled");
        group.Members.Single(m => m.Id == m1.Id).Status.Should().Be("Confirmed");

        visa = await a.Ok(new GetTravelVisaCaseByIdQuery(caseId));
        visa.Status.Should().Be("Submitted", "one applicant is still pending");
        visa.Applicants.Single(x => x.TravelerId == t2).Decision.Should().Be("Withdrawn");
        visa.Applicants.Single(x => x.TravelerId == t1).Decision.Should().Be("Pending");

        using (var db = fx.OpenDb())
        {
            // Both rows carry company A's tenant id and agree with each other — the same transaction wrote them.
            var rows = await db.QuerySingleAsync<(string MemberStatus, string Decision, int MemberTenant, int ApplicantTenant)>("""
                SELECT m.Status, ap.Decision, m.TenantId, ap.TenantId
                FROM TravelGroupMembers m
                INNER JOIN TravelVisaApplicants ap ON ap.TravelerId = m.TravelerId AND ap.TenantId = m.TenantId AND ap.VisaCaseId = @CaseId
                WHERE m.Id = @MemberId
                """, new { MemberId = m2.Id, CaseId = caseId });
            rows.MemberStatus.Should().Be("Cancelled");
            rows.Decision.Should().Be("Withdrawn");
            rows.MemberTenant.Should().Be(fx.A.TenantId);
            rows.ApplicantTenant.Should().Be(fx.A.TenantId);
        }

        var again = await a.Send(new CancelTravelGroupMemberCommand(groupId, m2.Id, new CancelTravelGroupMemberDto("Medical")));
        again.Success.Should().BeFalse("already cancelled");

        var manifest = await a.Ok(new GetTravelGroupManifestQuery(groupId));
        manifest.Pilgrims.Should().ContainSingle().Which.MemberId.Should().Be(m1.Id);
        manifest.VisaCaseNumber.Should().NotBeNullOrEmpty();

        // ── Company B ───────────────────────────────────────────────────────
        using var b = fx.Open(fx.B);
        await b.Invoking(s => s.Send(new GetTravelGroupByIdQuery(groupId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new GetTravelGroupManifestQuery(groupId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new GetTravelVisaCaseByIdQuery(caseId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new CancelTravelGroupMemberCommand(groupId, m1.Id, new CancelTravelGroupMemberDto("cross-tenant"))))
            .Should().ThrowAsync<NotFoundException>();
        (await a.Ok(new GetTravelGroupByIdQuery(groupId))).Members.Single(m => m.Id == m1.Id).Status.Should().Be("Confirmed", "company B changed nothing");
    }
}
