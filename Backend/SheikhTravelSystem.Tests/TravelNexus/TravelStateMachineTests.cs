using Dapper;
using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;
using A = SheikhTravelSystem.Application.Features.TravelNexus.Common.VisaCaseStateMachine.Actions;
using E = SheikhTravelSystem.Application.Features.TravelNexus.Common.EnquiryStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelStateMachineTests
{
    // ── Visa case: every (status, action) pair ─────────────────────────────

    public static TheoryData<string, string, string?> VisaTransitions()
    {
        var expected = new Dictionary<(string, string), string>
        {
            [("Draft", A.StartDocuments)] = "DocumentsPending",
            [("DocumentsPending", A.VerifyDocuments)] = "DocumentsVerified",
            [("DocumentsVerified", A.ReopenDocuments)] = "DocumentsPending",
            [("DocumentsVerified", A.Submit)] = "Submitted",
            [("Submitted", A.StartProcessing)] = "InProcessing",
        };
        foreach (var open in new[] { "Draft", "DocumentsPending", "DocumentsVerified", "Submitted", "InProcessing" })
        {
            expected[(open, A.Hold)] = "OnHold";
            expected[(open, A.Cancel)] = "Cancelled";
        }
        expected[("OnHold", A.Cancel)] = "Cancelled";
        expected[("OnHold", A.Resume)] = "Submitted"; // resolved from OnHoldFromStatus below

        var data = new TheoryData<string, string, string?>();
        var actions = new[] { A.StartDocuments, A.VerifyDocuments, A.ReopenDocuments, A.Submit, A.StartProcessing, A.Hold, A.Resume, A.Cancel };
        foreach (var status in TravelValues.VisaCaseStatuses)
            foreach (var action in actions)
                data.Add(status, action, expected.TryGetValue((status, action), out var t) ? t : null);
        return data;
    }

    [Theory]
    [MemberData(nameof(VisaTransitions))]
    public void Visa_case_transition_table_is_exactly_as_specified(string status, string action, string? expected)
        => VisaCaseStateMachine.Target(status, action, onHoldFromStatus: status == "OnHold" ? "Submitted" : null).Should().Be(expected);

    [Theory]
    [InlineData("Approved")]
    [InlineData("PartiallyApproved")]
    [InlineData("Rejected")]
    public void Outcome_statuses_are_never_reachable_by_a_direct_action(string outcome)
    {
        foreach (var status in TravelValues.VisaCaseStatuses)
            foreach (var action in VisaCaseStateMachine.TransitionActions.Append(A.Submit))
                VisaCaseStateMachine.Target(status, action, "Draft").Should().NotBe(outcome);
    }

    [Fact]
    public void Terminal_cases_allow_no_actions()
    {
        foreach (var status in VisaCaseStateMachine.TerminalStatuses)
            VisaCaseStateMachine.AllowedActions(status).Should().BeEmpty();
    }

    [Fact]
    public void Applicants_editable_only_before_submission_and_decisions_only_after()
    {
        VisaCaseStateMachine.AllowedActions("DocumentsVerified").Should().Contain(A.ManageApplicants).And.NotContain(A.RecordDecision);
        VisaCaseStateMachine.AllowedActions("Submitted").Should().Contain(A.RecordDecision).And.NotContain(A.ManageApplicants);
        VisaCaseStateMachine.AllowedActions("OnHold", "Submitted").Should().NotContain([A.RecordDecision, A.ManageApplicants]);
    }

    [Theory]
    [InlineData(new[] { "Approved", "Approved" }, "Approved")]
    [InlineData(new[] { "Rejected", "Rejected" }, "Rejected")]
    [InlineData(new[] { "Approved", "Rejected" }, "PartiallyApproved")]
    [InlineData(new[] { "Approved", "Withdrawn" }, "Approved")]
    [InlineData(new[] { "Approved", "Pending" }, null)]
    [InlineData(new[] { "Withdrawn", "Withdrawn" }, null)]
    [InlineData(new string[0], null)]
    public void Case_outcome_is_derived_from_applicant_decisions(string[] decisions, string? expected)
        => VisaCaseStateMachine.DeriveOutcome(decisions).Should().Be(expected);

    // ── Enquiry ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("New", E.MarkContacted, "Contacted")]
    [InlineData("Contacted", E.MarkContacted, null)]
    [InlineData("New", E.MarkQualified, "Qualified")]
    [InlineData("Quoted", E.MarkQualified, null)]
    [InlineData("Qualified", E.MarkQuoted, "Quoted")]
    [InlineData("Quoted", E.MarkLost, "Lost")]
    [InlineData("Won", E.MarkLost, null)]
    [InlineData("Lost", E.MarkLost, null)]
    [InlineData("Lost", E.Reopen, "Contacted")]
    [InlineData("Won", E.Reopen, null)]
    [InlineData("New", E.Convert, "Won")]
    [InlineData("Lost", E.Convert, null)]
    [InlineData("Won", E.Convert, null)]
    public void Enquiry_transitions(string status, string action, string? expected)
        => EnquiryStateMachine.Target(status, action).Should().Be(expected);

    [Fact]
    public void Won_is_only_reachable_through_convert()
    {
        foreach (var status in TravelValues.EnquiryStatuses)
            foreach (var action in EnquiryStateMachine.SimpleStatusActions)
                EnquiryStateMachine.Target(status, action).Should().NotBe("Won");
    }

    [Fact]
    public void Closed_enquiries_can_only_log_activity_or_reopen()
    {
        EnquiryStateMachine.AllowedActions("Won").Should().Equal(E.LogActivity);
        EnquiryStateMachine.AllowedActions("Lost").Should().Equal(E.LogActivity, E.Reopen);
    }

    // ── Wire values match state machines ───────────────────────────────────

    [Fact]
    public void State_machine_statuses_are_known_wire_values()
    {
        EnquiryStateMachine.OpenStatuses.Should().BeSubsetOf(TravelValues.EnquiryStatuses);
        VisaCaseStateMachine.TerminalStatuses.Should().BeSubsetOf(TravelValues.VisaCaseStatuses);
        VisaCaseStateMachine.PreSubmissionStatuses.Should().BeSubsetOf(TravelValues.VisaCaseStatuses);
    }

    // ── Numbering and date range helpers ───────────────────────────────────

    [Fact]
    public void Document_numbers_are_prefix_year_and_five_digit_sequence()
        => TravelNumberAllocator.Format("ENQ", 2026, 42).Should().Be("ENQ-2026-00042");

    [Fact]
    public void Date_range_is_inclusive_of_the_whole_to_day_and_always_applied()
    {
        var clauses = new List<string>();
        var p = new DynamicParameters();
        TravelSql.ApplyDateRange(new DateTime(2026, 11, 1, 15, 0, 0), new DateTime(2026, 11, 1), "e.CreatedAt", clauses, p);

        clauses.Should().Equal("e.CreatedAt >= @RangeFrom", "e.CreatedAt < @RangeToExclusive");
        p.Get<DateTime>("RangeFrom").Should().Be(new DateTime(2026, 11, 1));
        p.Get<DateTime>("RangeToExclusive").Should().Be(new DateTime(2026, 11, 2));
    }

    [Fact]
    public void Branch_scope_without_branches_only_sees_unassigned_records()
    {
        var clauses = new List<string>();
        TravelSql.ApplyBranchScope(new TravelBranchScope(false, []), "v.BranchId", clauses, new DynamicParameters());
        clauses.Should().Equal("v.BranchId IS NULL");
    }
}
