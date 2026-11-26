using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelReminderRulesTests
{
    private static readonly DateTime Now = new(2027, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TravelReminderDefinition Invoice = TravelReminderCatalog.Find("InvoiceDue")!;
    private static readonly TravelReminderDefinition Overdue = TravelReminderCatalog.Find("InvoiceOverdue")!;
    private static readonly TravelReminderDefinition FollowUp = TravelReminderCatalog.Find("EnquiryFollowUp")!;
    private static readonly TravelReminderDefinition Status = TravelReminderCatalog.Find("VisaStatusChanged")!;

    private static TravelReminderCandidate Candidate(DateTime due, string? stamp = null, string? customerName = "Ahmed Khan", string? passport = "ending 123")
        => new("Invoice", 31, "Invoice TINV-2027-00031", due, stamp, 7, 5, customerName, "+923001234567", "ahmed@example.com", "TINV-2027-00031", 60_000m, "PKR", passport, null);

    [Fact]
    public void Catalogue_matches_the_allowed_values_and_every_rule_has_a_default_lead()
    {
        TravelReminderCatalog.All.Select(d => d.RuleType).Should().BeEquivalentTo(TravelValues.ReminderRuleTypes);
        TravelReminderCatalog.All.Should().OnlyContain(d => d.DefaultLeads.Length > 0 && (d.SupportsStaff || d.SupportsCustomer));
        TravelReminderCatalog.All.Should().OnlyContain(d => TravelValues.ReminderEntityTypes.Contains(d.EntityType));
        TravelReminderCatalog.All.Where(d => d.Kind != TravelReminderKind.Upcoming).Should().OnlyContain(d => d.DefaultLeads.SequenceEqual(new[] { 0 }));
    }

    [Fact]
    public void Upcoming_rule_fires_while_the_trigger_is_within_the_lead_window_and_each_lead_once()
    {
        TravelReminderRules.IsDue(Invoice, 7, Now.AddDays(5), Now).Should().BeTrue();
        TravelReminderRules.IsDue(Invoice, 7, Now.AddDays(7).Date.AddHours(23), Now).Should().BeTrue("the whole last day counts");
        TravelReminderRules.IsDue(Invoice, 7, Now.AddDays(8), Now).Should().BeFalse();
        TravelReminderRules.IsDue(Invoice, 1, Now.AddDays(5), Now).Should().BeFalse("the 1-day lead is not due yet");
        TravelReminderRules.IsDue(Invoice, 7, Now.AddHours(-1), Now).Should().BeFalse("past triggers belong to the overdue rule");

        var c = Candidate(Now.AddDays(5));
        var key7 = TravelReminderRules.DedupeKey(Invoice, 7, c, "Customer", Now);
        var key1 = TravelReminderRules.DedupeKey(Invoice, 1, c, "Customer", Now.AddDays(4));
        key7.Should().Be("InvoiceDue:Invoice:31:2027-03-15:7:Customer");
        key1.Should().NotBe(key7);
        TravelReminderRules.DedupeKey(Invoice, 7, c, "Staff", Now).Should().NotBe(key7, "staff and customer are reminded separately");
        TravelReminderRules.DedupeKey(Invoice, 7, c, "Customer", Now.AddHours(5)).Should().Be(key7, "a later pass on the same trigger is a repeat");
    }

    [Fact]
    public void A_moved_due_date_fires_again_an_unchanged_one_does_not()
    {
        var before = TravelReminderRules.DedupeKey(Invoice, 7, Candidate(Now.AddDays(5)), "Customer", Now);
        var moved = TravelReminderRules.DedupeKey(Invoice, 7, Candidate(Now.AddDays(6)), "Customer", Now);
        moved.Should().NotBe(before);
    }

    [Fact]
    public void Overdue_rules_nudge_weekly_or_daily_and_never_before_the_trigger()
    {
        TravelReminderRules.IsDue(Overdue, 0, Now.AddDays(-10), Now).Should().BeTrue();
        TravelReminderRules.IsDue(Overdue, 0, Now.AddDays(1), Now).Should().BeFalse();
        var c = Candidate(Now.AddDays(-10));
        var thisWeek = TravelReminderRules.DedupeKey(Overdue, 0, c, "Customer", Now);
        thisWeek.Should().Be("InvoiceOverdue:Invoice:31:W2027-10:0:Customer");
        TravelReminderRules.DedupeKey(Overdue, 0, c, "Customer", Now.AddDays(2)).Should().Be(thisWeek, "same ISO week");
        TravelReminderRules.DedupeKey(Overdue, 0, c, "Customer", Now.AddDays(7)).Should().NotBe(thisWeek, "next week nudges again");
        var followUp = new TravelReminderCandidate("Enquiry", 9, "Enquiry ENQ-1", Now.AddHours(-3), null, 7, null, "Lead", null, null, "ENQ-1", null, null, null, null);
        TravelReminderRules.DedupeKey(FollowUp, 0, followUp, "Staff", Now).Should().EndWith(":D2027-03-10:0:Staff");
        TravelReminderRules.DedupeKey(FollowUp, 0, followUp, "Staff", Now.AddDays(1)).Should().EndWith(":D2027-03-11:0:Staff", "daily nudge");
    }

    [Fact]
    public void Event_rules_key_on_the_occurrence_stamp_and_look_back_48_hours()
    {
        var c = Candidate(Now.AddHours(-3), stamp: "H88");
        TravelReminderRules.IsDue(Status, 0, Now.AddHours(-3), Now).Should().BeTrue();
        TravelReminderRules.IsDue(Status, 0, Now.AddHours(-50), Now).Should().BeFalse();
        TravelReminderRules.DedupeKey(Status, 0, c, "Customer", Now).Should().Be("VisaStatusChanged:Invoice:31:H88:0:Customer");
        TravelReminderRules.DedupeKey(Status, 0, Candidate(Now.AddHours(-1), stamp: "H89"), "Customer", Now).Should().NotBe(TravelReminderRules.DedupeKey(Status, 0, c, "Customer", Now));
    }

    [Fact]
    public void Tenant_rows_replace_the_defaults_for_their_rule_type_only()
    {
        var rows = new List<TravelReminderRuleRow>
        {
            new("InvoiceDue", 3, true, true, true, true, false, false, "Pay {{Amount}} by {{Date}}"),
            new("InvoiceDue", 1, false, true, false, true, false, false, null)
        };
        var merged = TravelReminderRules.Merge(rows);
        var invoice = merged.Single(r => r.RuleType == "InvoiceDue");
        invoice.IsCustomised.Should().BeTrue();
        invoice.Leads.Select(l => l.LeadDays).Should().Equal(1, 3);
        invoice.CustomerTemplate.Should().Be("Pay {{Amount}} by {{Date}}");
        var passport = merged.Single(r => r.RuleType == "PassportExpiry");
        passport.IsCustomised.Should().BeFalse();
        passport.Leads.Select(l => l.LeadDays).Should().Equal(90, 30);
        passport.Leads.Should().OnlyContain(l => l.StaffInApp && l.CustomerEmail && !l.CustomerWhatsApp);

        var effective = TravelReminderRules.Effective(rows);
        effective.Where(e => e.Definition.RuleType == "InvoiceDue").Select(e => e.Lead.LeadDays).Should().Equal(3); // the disabled 1-day row is dropped
        effective.Should().Contain(e => e.Definition.RuleType == "TicketingDeadline" && e.Lead.LeadDays == 2, "untouched rules keep their defaults");
    }

    [Fact]
    public void Channels_a_rule_type_does_not_support_are_ignored()
    {
        var rows = new List<TravelReminderRuleRow> { new("EnquiryFollowUp", 0, true, true, false, true, true, true, null) };
        var lead = TravelReminderRules.Merge(rows).Single(r => r.RuleType == "EnquiryFollowUp").Leads.Single();
        lead.CustomerEmail.Should().BeFalse("follow-ups are staff-only");
        TravelReminderRules.CustomerChannels(lead).Should().BeEmpty();
        TravelReminderRules.StaffChannels(lead).Should().Equal("InApp");
    }

    [Fact]
    public void Quiet_hours_wrap_midnight()
    {
        TravelReminderRules.InQuietHours(new DateTime(2027, 3, 10, 22, 0, 0), 21, 8).Should().BeTrue();
        TravelReminderRules.InQuietHours(new DateTime(2027, 3, 10, 3, 0, 0), 21, 8).Should().BeTrue();
        TravelReminderRules.InQuietHours(new DateTime(2027, 3, 10, 8, 0, 0), 21, 8).Should().BeFalse();
        TravelReminderRules.InQuietHours(new DateTime(2027, 3, 10, 12, 0, 0), 21, 8).Should().BeFalse();
        TravelReminderRules.InQuietHours(new DateTime(2027, 3, 10, 13, 0, 0), 12, 14).Should().BeTrue();
        TravelReminderRules.InQuietHours(new DateTime(2027, 3, 10, 13, 0, 0), 9, 9).Should().BeFalse("equal hours mean no quiet window");
    }

    [Fact]
    public void Messages_use_iso_money_tenant_local_time_and_never_a_passport_number()
    {
        var karachi = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
        var appointment = TravelReminderCatalog.Find("VisaAppointment")!;
        var c = new TravelReminderCandidate("VisaCase", 9, "Visa case VC-2027-00009", new DateTime(2027, 3, 12, 5, 30, 0, DateTimeKind.Utc), null, 7, 5, "Ahmed Khan",
            "+92300", "a@b.c", "VC-2027-00009", null, null, "Saudi Arabia · Umrah", null);
        var customer = TravelReminderMessages.Build(appointment, c, "Customer", null, karachi);
        customer.Title.Should().Be("Visa appointment on 12 Mar 2027 at 10:30");
        customer.Body.Should().Contain("Dear Ahmed Khan").And.Contain("VC-2027-00009");

        var invoice = TravelReminderMessages.Build(Invoice, Candidate(new DateTime(2027, 3, 15)), "Staff", null, karachi);
        invoice.Body.Should().Contain("60,000.00 PKR").And.Contain("on 15 Mar 2027").And.NotContain("Rs");

        var passport = TravelReminderCatalog.Find("PassportExpiry")!;
        var traveler = new TravelReminderCandidate("Traveler", 4, "Passport ••••123 · Ahmed Khan", new DateTime(2027, 6, 1), null, 7, 5, "Ahmed Khan", null, "a@b.c",
            "Ahmed Khan", null, null, "ending 123", null);
        var text = TravelReminderMessages.Build(passport, traveler, "Customer", null, karachi);
        text.Body.Should().Contain("ending 123").And.NotContain("AB1234567").And.NotContain("CNIC");
    }

    [Fact]
    public void Customer_template_placeholders_are_substituted_and_nothing_else_interpreted()
    {
        var text = TravelReminderMessages.Build(Invoice, Candidate(new DateTime(2027, 3, 15)), "Customer", "Salam {{Name}}: {{Amount}} for {{Reference}} by {{Date}}. {{Unknown}} <b>x</b>",
            TimeZoneInfo.Utc);
        text.Body.Should().Be("Salam Ahmed Khan: 60,000.00 PKR for TINV-2027-00031 by on 15 Mar 2027. {{Unknown}} <b>x</b>");
    }
}
