using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.TravelNexus;

/// <summary>The dispatcher against an in-memory repository: claim once, send through the pipeline, record the outcome.</summary>
public class TravelReminderDispatcherTests
{
    private const int Tenant = 9;
    private static readonly DateTime Noon = new(2027, 3, 10, 7, 0, 0, DateTimeKind.Utc); // 12:00 in Karachi

    private readonly FakeReminderRepository _repo = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<ITenantTimeZoneResolver> _zones = new();
    private readonly Mock<IAuditEngine> _audit = new();
    private readonly List<NotificationCreateOptions> _sent = [];

    public TravelReminderDispatcherTests()
    {
        _config.Setup(c => c.GetSettingsAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelSettingsDto { ReminderQuietStartHour = 21, ReminderQuietEndHour = 8 });
        _zones.Setup(z => z.GetTenantTimeZoneIdAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync("Asia/Karachi");
        _notifications.Setup(n => n.CreateAndDispatchAsync(It.IsAny<NotificationCreateOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationCreateOptions, CancellationToken>((o, _) => _sent.Add(o)).ReturnsAsync(() => 1000 + _sent.Count);
    }

    private TravelReminderDispatcher Dispatcher(DateTime now) =>
        new(_repo, _config.Object, _notifications.Object, _zones.Object, _audit.Object, NullLogger<TravelReminderDispatcher>.Instance, new FixedClock(now));

    private static TravelReminderCandidate InvoiceDue(DateTime due, int? agent = 7, string? email = "khan@example.com")
        => new("Invoice", 31, "Invoice TINV-1", due, null, agent, 5, "Khan family", "+92300", email, "TINV-1", 60_000m, "PKR", null, null);

    [Fact]
    public async Task Sends_staff_and_customer_once_and_a_second_pass_sends_nothing()
    {
        _repo.Rules.Add(new TravelReminderRuleRow("InvoiceDue", 7, true, true, false, true, false, false, null));
        _repo.Candidates["InvoiceDue"] = [InvoiceDue(Noon.AddDays(5))];

        var first = await Dispatcher(Noon).RunTenantAsync(Tenant, default);
        first.Sent.Should().Be(2);
        _sent.Should().HaveCount(2);
        var staff = _sent.Single(s => s.UserId == 7);
        staff.Channels.Should().Equal("InApp");
        staff.TenantId.Should().Be(Tenant);
        staff.Type.Should().Be(NotificationType.TravelReminder);
        staff.Module.Should().Be("travel_reminder");
        var customer = _sent.Single(s => s.UserId is null);
        customer.Email.Should().Be("khan@example.com");
        customer.Channels.Should().Equal("Email");
        customer.RecipientType.Should().Be("Customer");
        _repo.Log.Should().OnlyContain(r => r.Status == "Sent" && r.NotificationId != null);

        var second = await Dispatcher(Noon.AddHours(2)).RunTenantAsync(Tenant, default);
        second.Sent.Should().Be(0);
        _sent.Should().HaveCount(2, "the dedupe key was already claimed");
        _audit.Verify(a => a.RecordAsync(It.Is<AuditEventWrite>(w => w.TenantId == Tenant && w.UserId == null && w.EventKey == "travel.reminder.dispatch"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_one_day_lead_fires_later_as_its_own_reminder()
    {
        _repo.Rules.Add(new TravelReminderRuleRow("InvoiceDue", 7, true, true, false, false, false, false, null));
        _repo.Rules.Add(new TravelReminderRuleRow("InvoiceDue", 1, true, true, false, false, false, false, null));
        _repo.Candidates["InvoiceDue"] = [InvoiceDue(Noon.AddDays(5))];

        (await Dispatcher(Noon).RunTenantAsync(Tenant, default)).Sent.Should().Be(1, "only the 7-day lead is due");
        (await Dispatcher(Noon.AddDays(4)).RunTenantAsync(Tenant, default)).Sent.Should().Be(1, "now the 1-day lead");
        (await Dispatcher(Noon.AddDays(4).AddHours(1)).RunTenantAsync(Tenant, default)).Sent.Should().Be(0);
        _repo.Log.Select(r => r.LeadDays).Should().BeEquivalentTo([7, 1]);
    }

    [Fact]
    public async Task No_assigned_agent_or_no_contact_is_recorded_as_skipped_with_the_reason()
    {
        _repo.Rules.Add(new TravelReminderRuleRow("InvoiceDue", 7, true, true, false, true, false, false, null));
        _repo.Candidates["InvoiceDue"] = [InvoiceDue(Noon.AddDays(5), agent: null, email: null)];

        var result = await Dispatcher(Noon).RunTenantAsync(Tenant, default);
        result.Sent.Should().Be(0);
        result.Skipped.Should().Be(2);
        _sent.Should().BeEmpty();
        _repo.Log.Should().Contain(r => r.Audience == "Staff" && r.Status == "Skipped" && r.FailureReason == TravelReminderDispatcher.NoAgentReason);
        _repo.Log.Should().Contain(r => r.Audience == "Customer" && r.Status == "Skipped" && r.FailureReason!.Contains("email"));
    }

    [Fact]
    public async Task A_provider_failure_is_recorded_with_its_reason_and_not_retried_on_the_next_pass()
    {
        _repo.Rules.Add(new TravelReminderRuleRow("InvoiceDue", 7, true, false, false, false, false, true, null));
        _repo.Candidates["InvoiceDue"] = [InvoiceDue(Noon.AddDays(5))];
        _notifications.Setup(n => n.CreateAndDispatchAsync(It.IsAny<NotificationCreateOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("WhatsApp template not approved"));

        var result = await Dispatcher(Noon).RunTenantAsync(Tenant, default);
        result.Failed.Should().Be(1);
        _repo.Log.Single().Status.Should().Be("Failed");
        _repo.Log.Single().FailureReason.Should().Contain("template not approved");
        (await Dispatcher(Noon.AddHours(1)).RunTenantAsync(Tenant, default)).Failed.Should().Be(0, "the claim stands; nothing loops");
    }

    [Fact]
    public async Task Customer_sends_are_held_in_quiet_hours_and_go_out_later_while_staff_are_not_held()
    {
        _repo.Rules.Add(new TravelReminderRuleRow("InvoiceDue", 7, true, true, false, true, false, false, null));
        _repo.Candidates["InvoiceDue"] = [InvoiceDue(Noon.AddDays(5))];
        var night = new DateTime(2027, 3, 9, 20, 0, 0, DateTimeKind.Utc); // 01:00 Karachi

        var held = await Dispatcher(night).RunTenantAsync(Tenant, default);
        held.Sent.Should().Be(1);
        held.Held.Should().Be(1);
        _sent.Single().UserId.Should().Be(7);

        var morning = await Dispatcher(night.AddHours(8)).RunTenantAsync(Tenant, default);
        morning.Sent.Should().Be(1);
        _sent.Last().UserId.Should().BeNull();
    }

    [Fact]
    public async Task A_tenant_with_every_rule_disabled_does_nothing()
    {
        foreach (var d in TravelReminderCatalog.All) _repo.Rules.Add(new TravelReminderRuleRow(d.RuleType, 0, false, false, false, false, false, false, null));
        _repo.Candidates["InvoiceDue"] = [InvoiceDue(Noon.AddDays(1))];
        var result = await Dispatcher(Noon).RunTenantAsync(Tenant, default);
        result.Candidates.Should().Be(0);
        _repo.CandidateCalls.Should().Be(0);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private sealed class FakeReminderRepository : ITravelReminderRepository
    {
        public List<TravelReminderRuleRow> Rules { get; } = [];
        public Dictionary<string, List<TravelReminderCandidate>> Candidates { get; } = [];
        public List<TravelReminderLogItemDto> Log { get; } = [];
        public int CandidateCalls { get; private set; }
        private readonly Dictionary<string, long> _keys = [];

        public Task<IReadOnlyList<TravelReminderRuleRow>> GetRulesAsync(int tenantId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TravelReminderRuleRow>>(Rules);
        public Task SaveRuleAsync(int tenantId, string ruleType, SaveTravelReminderRuleDto dto, int? userId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TravelReminderCandidate>> GetCandidatesAsync(int tenantId, string ruleType, DateTime fromUtc, DateTime toUtc, TravelBranchScope? scope = null, CancellationToken ct = default)
        {
            CandidateCalls++;
            var list = Candidates.GetValueOrDefault(ruleType, []).Where(c => c.DueAt >= fromUtc && c.DueAt <= toUtc).ToList();
            return Task.FromResult<IReadOnlyList<TravelReminderCandidate>>(list);
        }

        public Task<long?> TryClaimAsync(int tenantId, TravelReminderClaim claim, CancellationToken ct = default)
        {
            if (_keys.ContainsKey(claim.DedupeKey)) return Task.FromResult<long?>(null);
            var id = Log.Count + 1L;
            _keys[claim.DedupeKey] = id;
            Log.Add(new TravelReminderLogItemDto { Id = id, RuleType = claim.RuleType, LeadDays = claim.LeadDays, Audience = claim.Audience, Status = "Pending", Title = claim.Title, Body = claim.Body });
            return Task.FromResult<long?>(id);
        }

        public Task MarkAsync(int tenantId, long id, string status, int? notificationId, string? failureReason, CancellationToken ct = default)
        {
            var row = Log.Single(r => r.Id == id);
            row.Status = status; row.NotificationId = notificationId; row.FailureReason = failureReason;
            return Task.CompletedTask;
        }

        public Task<Application.Common.PagedResult<TravelReminderLogItemDto>> GetLogAsync(int tenantId, TravelReminderLogFilter filter, int page, int pageSize, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, DateTime sinceUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountSkippedNoAgentAsync(int tenantId, DateTime sinceUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> GetActiveTenantIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([Tenant]);
    }
}
