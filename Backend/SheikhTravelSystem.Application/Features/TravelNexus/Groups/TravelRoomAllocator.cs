using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Groups;

/// <summary>
/// Rooming rules for one hotel stay (pure).
/// <list type="bullet">
/// <item>A <b>family</b> is a set of pilgrims linked by a mahram relation, plus children/infants linked to the adults of
/// the same booking customer. Families share Family rooms.</item>
/// <item>Everyone else shares rooms with pilgrims of the same gender (Male / Female rooms).</item>
/// <item>No room exceeds its type's capacity; every pilgrim has at most one bed per stay.</item>
/// </list>
/// </summary>
public static class TravelRoomAllocator
{
    /// <summary>Suggested rooms for all active pilgrims. Nothing is saved — the user reviews and saves.</summary>
    public static IReadOnlyList<TravelRoomingRoomDto> Suggest(IReadOnlyList<TravelGroupMemberFacts> members)
    {
        var active = members.Where(m => m.Status != "Cancelled").ToList();
        var families = FamilyUnits(active);
        var rooms = new List<TravelRoomingRoomDto>();
        var placed = new HashSet<int>();

        foreach (var unit in families.Where(u => u.Count > 1))
        {
            var type = MostCommon(unit.Select(m => m.RoomPreference));
            var capacity = TravelValues.RoomCapacity(type);
            // Adults first so each chunk keeps a guardian with the children.
            var ordered = unit.OrderBy(m => m.PriceCategory == "Adult" ? 0 : 1).ThenBy(m => m.MemberId).ToList();
            foreach (var chunk in ordered.Chunk(capacity))
            {
                rooms.Add(new TravelRoomingRoomDto(null, type, "Family", chunk.Select(m => m.MemberId).ToList()));
                placed.UnionWith(chunk.Select(m => m.MemberId));
            }
        }

        foreach (var g in active.Where(m => !placed.Contains(m.MemberId))
                     .GroupBy(m => (m.Gender, m.RoomPreference))
                     .OrderBy(g => g.Key.Gender).ThenBy(g => g.Key.RoomPreference))
        {
            foreach (var chunk in g.OrderBy(m => m.MemberId).Chunk(TravelValues.RoomCapacity(g.Key.RoomPreference)))
                rooms.Add(new TravelRoomingRoomDto(null, g.Key.RoomPreference, g.Key.Gender, chunk.Select(m => m.MemberId).ToList()));
        }

        return rooms;
    }

    /// <summary>Null when the rooming plan is valid; otherwise a user-facing reason.</summary>
    public static string? Validate(IReadOnlyList<TravelRoomingRoomDto> rooms, IReadOnlyList<TravelGroupMemberFacts> members)
    {
        var active = members.Where(m => m.Status != "Cancelled").ToDictionary(m => m.MemberId);
        var familyOf = new Dictionary<int, int>();
        var units = FamilyUnits(active.Values.ToList());
        for (var i = 0; i < units.Count; i++)
            foreach (var m in units[i]) familyOf[m.MemberId] = i;

        var seen = new HashSet<int>();
        for (var r = 0; r < rooms.Count; r++)
        {
            var room = rooms[r];
            var label = string.IsNullOrWhiteSpace(room.RoomNumber) ? $"Room {r + 1}" : $"Room {room.RoomNumber}";
            if (!TravelValues.IsOneOf(room.RoomType, TravelValues.RoomTypes)) return $"{label}: unknown room type.";
            if (!TravelValues.IsOneOf(room.GenderPolicy, TravelValues.GenderPolicies)) return $"{label}: unknown room policy.";
            if (room.MemberIds.Count > TravelValues.RoomCapacity(room.RoomType))
                return $"{label}: a {room.RoomType} room holds at most {TravelValues.RoomCapacity(room.RoomType)}.";

            foreach (var id in room.MemberIds)
            {
                if (!active.ContainsKey(id)) return $"{label}: a pilgrim is not active in this group.";
                if (!seen.Add(id)) return $"{label}: a pilgrim is placed in more than one room for this stay.";
            }

            var occupants = room.MemberIds.Select(id => active[id]).ToList();
            switch (room.GenderPolicy)
            {
                case "Male" when occupants.Any(m => m.Gender != "Male"):
                    return $"{label}: a men's room can only hold men.";
                case "Female" when occupants.Any(m => m.Gender != "Female"):
                    return $"{label}: a women's room can only hold women.";
                case "Family" when occupants.Select(m => familyOf[m.MemberId]).Distinct().Count() > 1:
                    return $"{label}: a family room can only hold one family (linked by mahram or as children of the same booking).";
            }
        }
        return null;
    }

    /// <summary>Connected components over mahram links and child→adult links within the same customer.</summary>
    public static List<List<TravelGroupMemberFacts>> FamilyUnits(IReadOnlyList<TravelGroupMemberFacts> members)
    {
        var parent = members.ToDictionary(m => m.MemberId, m => m.MemberId);
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(int a, int b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }

        var byTraveler = members.ToDictionary(m => m.TravelerId);
        foreach (var m in members)
        {
            if (m.MahramTravelerId is int mahram && byTraveler.TryGetValue(mahram, out var guardian))
                Union(m.MemberId, guardian.MemberId);
        }
        foreach (var child in members.Where(m => m.PriceCategory != "Adult"))
        {
            foreach (var adult in members.Where(a => a.PriceCategory == "Adult" && a.CustomerId == child.CustomerId))
                Union(child.MemberId, adult.MemberId);
        }

        return members.GroupBy(m => Find(m.MemberId)).Select(g => g.ToList()).ToList();
    }

    private static string MostCommon(IEnumerable<string> values)
        => values.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenByDescending(g => TravelValues.RoomCapacity(g.Key)).First().Key;
}

/// <summary>Readiness for "Mark ready": every confirmed pilgrim has an approved visa and a bed in every stay.</summary>
public static class TravelGroupReadiness
{
    /// <summary>
    /// <paramref name="tickets"/>: issued tickets on ticketed flights of the group's booking files. Every confirmed pilgrim needs
    /// a round trip, or an outbound and a return flight (Phase 5 flight confirmation).
    /// </summary>
    public static TravelGroupReadinessDto Evaluate(IReadOnlyList<TravelGroupMemberDto> members, string? visaCaseStatus,
        IReadOnlyList<TravelGroupStayDto> stays, IReadOnlyList<TravelGroupRoomDto> rooms,
        IReadOnlyList<Bookings.DTOs.TravelGroupTicketFact> tickets)
    {
        var confirmed = members.Where(m => m.Status == "Confirmed").ToList();
        var assignedByStay = rooms.GroupBy(r => r.GroupHotelId)
            .ToDictionary(g => g.Key, g => g.SelectMany(r => r.MemberIds).ToHashSet());
        var withoutRoom = confirmed.Count(m => stays.Any(s =>
            !assignedByStay.TryGetValue(s.Id, out var ids) || !ids.Contains(m.Id)));

        return new TravelGroupReadinessDto
        {
            ConfirmedMembers = confirmed.Count,
            UnconfirmedMembers = members.Count(m => m.Status == "Registered"),
            MembersWithApprovedVisa = confirmed.Count(m => m.VisaDecision == "Approved"),
            MembersWithoutRoom = withoutRoom,
            MembersWithoutTickets = Bookings.TravelBookingRules.UncoveredTravelers(confirmed.Select(m => m.TravelerId), tickets).Count,
            Stays = stays.Count,
            VisaCaseDecided = visaCaseStatus is "Approved" or "PartiallyApproved"
        };
    }
}
