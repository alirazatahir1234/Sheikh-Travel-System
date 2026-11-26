using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;
using A = SheikhTravelSystem.Application.Features.TravelNexus.Groups.TravelGroupStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelGroupRulesTests
{
    // ── State machine ──────────────────────────────────────────────────────

    public static TheoryData<string, string, string?> Transitions()
    {
        var expected = new Dictionary<(string, string), string>
        {
            [("Draft", A.Open)] = "Open",
            [("Open", A.CloseRegistration)] = "RegistrationClosed",
            [("RegistrationClosed", A.Reopen)] = "Open",
            [("RegistrationClosed", A.StartVisaProcessing)] = "VisaProcessing",
            [("VisaProcessing", A.MarkReady)] = "Ready",
            [("Ready", A.Unready)] = "VisaProcessing",
            [("Ready", A.Depart)] = "Departed",
            [("Departed", A.Return)] = "Returned",
            [("Returned", A.Close)] = "Closed",
        };
        foreach (var s in new[] { "Draft", "Open", "RegistrationClosed", "VisaProcessing", "Ready" })
            expected[(s, A.Cancel)] = "Cancelled";

        var data = new TheoryData<string, string, string?>();
        foreach (var status in TravelValues.GroupStatuses)
            foreach (var action in TravelGroupStateMachine.TransitionActions)
                data.Add(status, action, expected.TryGetValue((status, action), out var t) ? t : null);
        return data;
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void Group_transition_table_is_exactly_as_specified(string status, string action, string? expected)
        => TravelGroupStateMachine.Target(status, action).Should().Be(expected);

    [Fact]
    public void Registration_only_while_open_and_rooms_until_departure()
    {
        TravelGroupStateMachine.AllowedActions("Open").Should().Contain(A.RegisterMembers);
        TravelGroupStateMachine.AllowedActions("RegistrationClosed").Should().NotContain(A.RegisterMembers);
        TravelGroupStateMachine.AllowedActions("Ready").Should().Contain(A.ManageRooms);
        TravelGroupStateMachine.AllowedActions("Departed").Should().NotContain([A.ManageRooms, A.ManageMembers, A.Cancel]);
        TravelGroupStateMachine.AllowedActions("Closed").Should().BeEmpty();
    }

    // ── Room allocation ────────────────────────────────────────────────────

    private static TravelGroupMemberFacts M(int id, string gender, int customer, string category = "Adult", string room = "Quad", int? mahram = null, string status = "Confirmed")
        => new(id, 100 + id, customer, gender, category, room, mahram is null ? null : 100 + mahram, status);

    private static readonly TravelGroupMemberFacts[] Pilgrims =
    [
        M(1, "Male", 10),                         // husband
        M(2, "Female", 10, mahram: 1),            // wife
        M(3, "Male", 10, "Child"),                // their son
        M(4, "Female", 10, "Infant"),             // their baby
        M(5, "Male", 20, room: "Double"),         // single man
        M(6, "Male", 21, room: "Double"),         // single man
        M(7, "Female", 30, mahram: 8),            // woman with mahram (brother)
        M(8, "Male", 30),                         // her brother
        M(9, "Female", 40),                       // single woman
        M(10, "Male", 50, status: "Cancelled"),   // cancelled
    ];

    [Fact]
    public void Families_share_family_rooms_and_others_are_split_by_gender()
    {
        var rooms = TravelRoomAllocator.Suggest(Pilgrims);

        rooms.Should().ContainSingle(r => r.GenderPolicy == "Family" && r.MemberIds.OrderBy(i => i).SequenceEqual(new[] { 1, 2, 3, 4 }));
        rooms.Should().ContainSingle(r => r.GenderPolicy == "Family" && r.MemberIds.OrderBy(i => i).SequenceEqual(new[] { 7, 8 }));
        rooms.Should().ContainSingle(r => r.GenderPolicy == "Male" && r.RoomType == "Double" && r.MemberIds.OrderBy(i => i).SequenceEqual(new[] { 5, 6 }));
        rooms.Should().ContainSingle(r => r.GenderPolicy == "Female" && r.MemberIds.SequenceEqual(new[] { 9 }));
        rooms.SelectMany(r => r.MemberIds).Should().NotContain(10).And.OnlyHaveUniqueItems();
        TravelRoomAllocator.Validate(rooms, Pilgrims).Should().BeNull();
    }

    [Fact]
    public void Rooms_never_exceed_capacity()
    {
        var men = Enumerable.Range(1, 9).Select(i => M(i, "Male", 100 + i, room: "Triple")).ToList();
        var rooms = TravelRoomAllocator.Suggest(men);
        rooms.Should().HaveCount(3).And.OnlyContain(r => r.MemberIds.Count <= 3);
    }

    [Theory]
    [InlineData("Female", new[] { 5 }, "women")]               // man in a women's room
    [InlineData("Family", new[] { 1, 7 }, "one family")]       // two families in one room
    [InlineData("Male", new[] { 10 }, "not active")]           // cancelled pilgrim
    public void Validation_rejects_wrong_occupants(string policy, int[] members, string expected)
        => TravelRoomAllocator.Validate([new TravelRoomingRoomDto("101", "Quad", policy, members)], Pilgrims).Should().Contain(expected);

    [Fact]
    public void Validation_rejects_overfull_rooms_and_double_booking()
    {
        TravelRoomAllocator.Validate([new TravelRoomingRoomDto("101", "Double", "Male", [1, 5, 6])], Pilgrims).Should().Contain("at most 2");
        TravelRoomAllocator.Validate(
            [new TravelRoomingRoomDto("101", "Double", "Male", [5]), new TravelRoomingRoomDto("102", "Double", "Male", [5])], Pilgrims)
            .Should().Contain("more than one room");
    }

    // ── Readiness & planning ───────────────────────────────────────────────

    [Fact]
    public void Ready_requires_visa_approval_and_a_bed_in_every_stay()
    {
        var members = new List<TravelGroupMemberDto>
        {
            new() { Id = 1, TravelerId = 101, Status = "Confirmed", VisaDecision = "Approved" },
            new() { Id = 2, TravelerId = 102, Status = "Confirmed", VisaDecision = "Approved" },
            new() { Id = 3, TravelerId = 103, Status = "Cancelled" }
        };
        var stays = new List<TravelGroupStayDto> { new() { Id = 11 }, new() { Id = 12 } };
        var rooms = new List<TravelGroupRoomDto>
        {
            new() { GroupHotelId = 11, MemberIds = [1, 2] },
            new() { GroupHotelId = 12, MemberIds = [1] }
        };
        var tickets = new List<TravelGroupTicketFact> { new(101, "RoundTrip"), new(102, "Outbound"), new(102, "Return") };

        var r = TravelGroupReadiness.Evaluate(members, "Approved", stays, rooms, tickets);
        r.MembersWithoutRoom.Should().Be(1);
        r.IsReady.Should().BeFalse();

        rooms.Add(new TravelGroupRoomDto { GroupHotelId = 12, MemberIds = [2] });
        TravelGroupReadiness.Evaluate(members, "Approved", stays, rooms, tickets).IsReady.Should().BeTrue();
        TravelGroupReadiness.Evaluate(members, "Submitted", stays, rooms, tickets).IsReady.Should().BeFalse();
        members[1].VisaDecision = "Rejected";
        TravelGroupReadiness.Evaluate(members, "PartiallyApproved", stays, rooms, tickets).MembersWithoutVisa.Should().Be(1);
    }

    [Fact]
    public void Ready_requires_every_confirmed_pilgrim_ticketed_out_and_back()
    {
        var members = new List<TravelGroupMemberDto>
        {
            new() { Id = 1, TravelerId = 101, Status = "Confirmed", VisaDecision = "Approved" },
            new() { Id = 2, TravelerId = 102, Status = "Confirmed", VisaDecision = "Approved" },
            new() { Id = 3, TravelerId = 103, Status = "Cancelled" }
        };
        var stays = new List<TravelGroupStayDto> { new() { Id = 11 } };
        var rooms = new List<TravelGroupRoomDto> { new() { GroupHotelId = 11, MemberIds = [1, 2] } };

        // 102 is only ticketed outbound; the cancelled pilgrim does not count.
        var partial = TravelGroupReadiness.Evaluate(members, "Approved", stays, rooms, [new(101, "RoundTrip"), new(102, "Outbound")]);
        partial.MembersWithoutTickets.Should().Be(1);
        partial.IsReady.Should().BeFalse();

        TravelGroupReadiness.Evaluate(members, "Approved", stays, rooms, []).MembersWithoutTickets.Should().Be(2);
        TravelGroupReadiness.Evaluate(members, "Approved", stays, rooms,
            [new(101, "RoundTrip"), new(102, "Outbound"), new(102, "Return")]).IsReady.Should().BeTrue();
    }

    [Fact]
    public void Package_stays_become_consecutive_dates_from_departure()
    {
        var stays = TravelGroupPlanning.StaysFromPackage(
        [
            new TravelPackageStayDto { HotelId = 1, Nights = 10, Sequence = 1 },
            new TravelPackageStayDto { HotelId = 2, Nights = 4, Sequence = 2 }
        ], new DateTime(2027, 1, 10));

        stays[0].Should().Be(new TravelGroupStayPlan(1, new DateTime(2027, 1, 10), new DateTime(2027, 1, 20), 1));
        stays[1].Should().Be(new TravelGroupStayPlan(2, new DateTime(2027, 1, 20), new DateTime(2027, 1, 24), 2));
    }

    [Fact]
    public void Summary_counts_cover_every_status_and_sum_to_total()
    {
        var s = GetTravelGroupSummaryQueryHandler.Build(new Dictionary<string, int> { ["Open"] = 2, ["Closed"] = 3 });
        s.Counts.Keys.Should().BeEquivalentTo(TravelValues.GroupStatuses);
        s.Counts.Values.Sum().Should().Be(s.Total).And.Be(5);
    }

    [Fact]
    public void Manifest_masks_passports_without_pii_permission_and_lists_rooms_per_stay()
    {
        var members = new List<TravelGroupMemberDto>
        {
            new() { Id = 1, TravelerId = 101, TravelerName = "ALI KHAN", Gender = "Male", PassportNumber = "AB1234567", CustomerName = "Khan family", Status = "Confirmed" },
            new() { Id = 2, TravelerId = 102, TravelerName = "SARA KHAN", Gender = "Female", PassportNumber = "CD7654321", CustomerName = "Khan family", Status = "Confirmed" }
        };
        var travelers = new Dictionary<int, Application.Features.TravelNexus.Travelers.DTOs.TravelTravelerDto>
        {
            [101] = new() { Id = 101, GivenNames = "ALI", Surname = "KHAN" },
            [102] = new() { Id = 102, GivenNames = "SARA", Surname = "KHAN", MahramTravelerId = 101 }
        };
        var stays = new List<TravelGroupStayDto> { new() { Id = 11, City = "Makkah", HotelName = "Hilton", Sequence = 1 } };
        var rooms = new List<TravelGroupRoomDto> { new() { GroupHotelId = 11, RoomNumber = "512", RoomType = "Double", MemberIds = [1, 2] } };

        var rows = TravelGroupManifest.BuildPilgrims(members, travelers, stays, rooms, canViewPii: false);

        rows.Should().HaveCount(2).And.OnlyContain(r => r.PassportNumber!.StartsWith(TravelPii.MaskChar));
        rows.Single(r => r.GivenNames == "SARA").MahramName.Should().Be("ALI KHAN");
        rows[0].Rooms.Should().Equal("Makkah · Hilton · Room 512 (Double)");
        TravelGroupManifest.BuildPilgrims(members, travelers, stays, rooms, canViewPii: true)[0].PassportNumber.Should().Be("AB1234567");
    }
}
