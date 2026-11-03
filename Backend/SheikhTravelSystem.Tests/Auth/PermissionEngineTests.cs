using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Authentication;

namespace SheikhTravelSystem.Tests.Auth;

public class PermissionEngineTests
{
    private static readonly string[] CoreAndBookingPermissions =
    [
        FleetPermissions.VehicleView,
        DriverPermissions.DriverView,
        AnalyticsPermissions.GpsView,
        GpsPermissions.AlertView,
        MaintenancePermissions.View,
        NotificationPermissions.View,
        OperationsPermissions.TripView,
        OperationsPermissions.BookingView,
        OperationsPermissions.RouteView,
        FinancePermissions.FuelView,
    ];

    [Fact]
    public void PermissionRegistrySeed_never_overloads_FeatureKey_for_catalog_modules()
    {
        foreach (var entry in PermissionRegistrySeed.All)
        {
            entry.FeatureKey.Should().BeNull(
                $"catalog permission '{entry.PermissionCode}' (ModuleKey='{entry.ModuleKey}') must be governed by TenantModules, not TenantFeatures");
        }

        PermissionRegistrySeed.Find(OperationsPermissions.BookingView)!.ModuleKey.Should().Be("BOOKINGS");
        PermissionRegistrySeed.Find(OperationsPermissions.TripView)!.ModuleKey.Should().Be("TRAVEL");
        PermissionRegistrySeed.Find(FleetPermissions.VehicleView)!.ModuleKey.Should().Be("FLEET");
        PermissionRegistrySeed.Find(AnalyticsPermissions.GpsView)!.ModuleKey.Should().Be("GPS");
        PermissionRegistrySeed.Find(NotificationPermissions.View)!.ModuleKey.Should().BeNull();
    }

    [Fact]
    public void Owner_role_template_includes_Booking_View_and_Trip_View()
    {
        TenantRolePermissionTemplates.Owner.Should().Contain(OperationsPermissions.BookingView);
        TenantRolePermissionTemplates.Owner.Should().Contain(OperationsPermissions.TripView);
        TenantRolePermissionTemplates.Owner.Should().Contain(FleetPermissions.VehicleView);
        TenantRolePermissionTemplates.Owner.Should().Contain(AnalyticsPermissions.GpsView);
    }

    [Fact]
    public async Task EvaluateAsync_with_0_features_enabled_keeps_core_fleet_gps_and_booking_permissions()
    {
        var logger = new CapturingLogger<PermissionEngine>();
        var dbFactory = CreateDbFactory(
            roleCodes: ["OWNER"],
            enabledModules: ["DASHBOARD", "FLEET", "GPS", "TRAVEL", "BOOKINGS", "FINANCE", "ANALYTICS", "AI"],
            enabledFeatures: []);

        var engine = new PermissionEngine(dbFactory, logger);
        var result = await engine.EvaluateAsync(userId: 10, tenantId: 2);

        result.PermissionCodes.Should().Contain(
        [
            FleetPermissions.VehicleView,
            AnalyticsPermissions.GpsView,
            MaintenancePermissions.View,
            NotificationPermissions.View,
            OperationsPermissions.TripView,
            OperationsPermissions.BookingView,
        ]);
        logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData("rental")]
    [InlineData("bookings")]
    public async Task EvaluateAsync_with_1_feature_enabled_never_strips_core_fleet_gps_maintenance_notification_trip_or_booking_permissions(
        string singleEnabledFeature)
    {
        var logger = new CapturingLogger<PermissionEngine>();
        // Simulate legacy DB Permissions rows that still carry stale FeatureKey values
        // ("vehicles", "gps-tracking", "maintenance", "trips", "bookings") to verify both
        // seed cleanup and legacy DB defense-in-depth.
        var legacyDbGates = new Dictionary<string, (string? ModuleKey, string? FeatureKey)>(StringComparer.OrdinalIgnoreCase)
        {
            [FleetPermissions.VehicleView] = ("FLEET", "vehicles"),
            [AnalyticsPermissions.GpsView] = ("GPS", "gps-tracking"),
            [MaintenancePermissions.View] = ("FLEET", "maintenance"),
            [OperationsPermissions.TripView] = ("BOOKINGS", "trips"),
            [OperationsPermissions.BookingView] = ("BOOKINGS", "bookings"),
            [NotificationPermissions.View] = ("NOTIFICATIONS", null),
        };

        var enabledModules = singleEnabledFeature == "bookings"
            ? new[] { "DASHBOARD", "FLEET", "GPS", "TRAVEL" } // BOOKINGS implied via TenantFeatures "bookings"
            : new[] { "DASHBOARD", "FLEET", "GPS", "TRAVEL", "BOOKINGS", "RENTAL" };

        var dbFactory = CreateDbFactory(
            roleCodes: ["OWNER"],
            enabledModules: enabledModules,
            enabledFeatures: [singleEnabledFeature],
            permissionGates: legacyDbGates);

        var engine = new PermissionEngine(dbFactory, logger);
        var result = await engine.EvaluateAsync(userId: 15, tenantId: 3);

        result.PermissionCodes.Should().Contain(
        [
            FleetPermissions.VehicleView,
            AnalyticsPermissions.GpsView,
            MaintenancePermissions.View,
            NotificationPermissions.View,
            OperationsPermissions.TripView,
            OperationsPermissions.BookingView,
        ]);
    }

    [Fact]
    public async Task EvaluateAsync_with_all_features_and_modules_enabled_keeps_all_granted_permissions()
    {
        var logger = new CapturingLogger<PermissionEngine>();
        var allModules = TenantModuleCatalog.All.Select(m => m.Code).ToArray();
        var allFeatures = FeatureRegistrySeed.All.Select(f => f.FeatureKey).ToArray();

        var dbFactory = CreateDbFactory(
            roleCodes: ["TENANT_ADMIN"],
            rolePermissions: TenantRolePermissionTemplates.TenantAdmin,
            enabledModules: allModules,
            enabledFeatures: allFeatures);

        var engine = new PermissionEngine(dbFactory, logger);
        var result = await engine.EvaluateAsync(userId: 20, tenantId: 4);

        var expected = TenantRolePermissionTemplates.TenantAdmin
            .Where(p => !PermissionAuthorizationHandler.IsSuperAdminOnlyPermission(p))
            .ToArray();

        result.PermissionCodes.Should().Contain(expected);
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterByTenantEntitlementsAsync_with_disabled_catalog_module_strips_only_that_module_and_logs_reason()
    {
        var logger = new CapturingLogger<PermissionEngine>();
        // Enable FLEET, GPS, TRAVEL; disable BOOKINGS and AI.
        var dbFactory = CreateDbFactory(
            roleCodes: ["OWNER"],
            enabledModules: ["DASHBOARD", "FLEET", "GPS", "TRAVEL"],
            enabledFeatures: []);

        var engine = new PermissionEngine(dbFactory, logger);
        using var connection = dbFactory.CreateConnection();

        var input = CoreAndBookingPermissions.Concat([AiPermissions.View]).ToList();
        var kept = await engine.FilterByTenantEntitlementsAsync(
            connection,
            tenantId: 7,
            permissionCodes: input,
            userId: 99);

        // Bookings and AI are stripped because BOOKINGS and AI modules are disabled.
        kept.Should().NotContain(OperationsPermissions.BookingView);
        kept.Should().NotContain(AiPermissions.View);

        // Core fleet, GPS, maintenance, notifications, and Trip.View survive!
        kept.Should().Contain(
        [
            FleetPermissions.VehicleView,
            DriverPermissions.DriverView,
            AnalyticsPermissions.GpsView,
            GpsPermissions.AlertView,
            MaintenancePermissions.View,
            NotificationPermissions.View,
            OperationsPermissions.TripView,
            OperationsPermissions.RouteView,
            FinancePermissions.FuelView,
        ]);

        logger.Entries.Should().ContainSingle();
        var log = logger.Entries[0];
        log.Level.Should().Be(LogLevel.Warning);
        log.Message.Should().Contain("StrippedByDisabledModule");
        log.Message.Should().Contain("ModuleKey=BOOKINGS");
        log.Message.Should().Contain("ModuleKey=AI");
        log.Message.Should().Contain(OperationsPermissions.BookingView);
        log.Message.Should().Contain(AiPermissions.View);
        log.Message.Should().Contain("TenantId=7");
        log.Message.Should().Contain("UserId=99");
    }

    [Fact]
    public void FilterByTenantEntitlements_with_disabled_opt_in_feature_strips_and_logs_StrippedByDisabledFeature()
    {
        var logger = new CapturingLogger<PermissionEngine>();
        var engine = new PermissionEngine(Mock.Of<IDbConnectionFactory>(), logger);

        var customGates = new Dictionary<string, (string? ModuleKey, string? FeatureKey)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Custom.BetaAddon.Use"] = (null, "beta-addon"),
        };

        var kept = engine.FilterByTenantEntitlements(
            tenantId: 9,
            permissionCodes: [FleetPermissions.VehicleView, AnalyticsPermissions.GpsView, "Custom.BetaAddon.Use"],
            enabledModules: new HashSet<string>(["FLEET", "GPS"], StringComparer.OrdinalIgnoreCase),
            enabledFeatures: new HashSet<string>(["rental"], StringComparer.OrdinalIgnoreCase),
            permissionGates: customGates,
            userId: 42);

        kept.Should().Contain([FleetPermissions.VehicleView, AnalyticsPermissions.GpsView]);
        kept.Should().NotContain("Custom.BetaAddon.Use");

        logger.Entries.Should().ContainSingle();
        var log = logger.Entries[0];
        log.Level.Should().Be(LogLevel.Warning);
        log.Message.Should().Contain("StrippedByDisabledFeature");
        log.Message.Should().Contain("FeatureKey=beta-addon");
        log.Message.Should().Contain("Custom.BetaAddon.Use");
        log.Message.Should().Contain("TenantId=9");
        log.Message.Should().Contain("UserId=42");
    }

    private static IDbConnectionFactory CreateDbFactory(
        IReadOnlyList<string> roleCodes,
        IReadOnlyList<string> enabledModules,
        IReadOnlyList<string> enabledFeatures,
        IReadOnlyList<string>? rolePermissions = null,
        IReadOnlyDictionary<string, (string? ModuleKey, string? FeatureKey)>? permissionGates = null)
    {
        var factory = new Mock<IDbConnectionFactory>();
        factory
            .Setup(f => f.CreateConnection())
            .Returns(() => new FakePermissionDbConnection(
                roleCodes,
                enabledModules,
                enabledFeatures,
                rolePermissions,
                permissionGates));
        return factory.Object;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private sealed class FakePermissionDbConnection(
        IReadOnlyList<string> roleCodes,
        IReadOnlyList<string> enabledModules,
        IReadOnlyList<string> enabledFeatures,
        IReadOnlyList<string>? rolePermissions,
        IReadOnlyDictionary<string, (string? ModuleKey, string? FeatureKey)>? permissionGates) : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "FakeDb";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() =>
            new FakePermissionDbCommand(this, roleCodes, enabledModules, enabledFeatures, rolePermissions, permissionGates);
    }

    private sealed class FakePermissionDbCommand(
        DbConnection connection,
        IReadOnlyList<string> roleCodes,
        IReadOnlyList<string> enabledModules,
        IReadOnlyList<string> enabledFeatures,
        IReadOnlyList<string>? rolePermissions,
        IReadOnlyDictionary<string, (string? ModuleKey, string? FeatureKey)>? permissionGates) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }
        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar()
        {
            var sql = CommandText ?? "";
            if (sql.Contains("SELECT Role FROM Users", StringComparison.OrdinalIgnoreCase))
                return null;
            if (sql.Contains("UPPER(r.Code) = N'SUPER_ADMIN'", StringComparison.OrdinalIgnoreCase))
                return roleCodes.Contains(PlatformRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase) ? 1 : null;
            return null;
        }

        protected override DbParameter CreateDbParameter() => new FakeDbParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var sql = CommandText ?? "";
            var table = new DataTable();

            if (sql.Contains("FROM UserRoles ur", StringComparison.OrdinalIgnoreCase)
                && sql.Contains("r.Id AS RoleId", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("Code", typeof(string));
                table.Columns.Add("RoleId", typeof(int));
                for (var i = 0; i < roleCodes.Count; i++)
                    table.Rows.Add(roleCodes[i], i + 1);
            }
            else if (sql.Contains("FROM Roles r", StringComparison.OrdinalIgnoreCase)
                     && sql.Contains("INNER JOIN RolePermissions rp", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("PermissionCode", typeof(string));
                table.Columns.Add("RoleCode", typeof(string));
                if (rolePermissions is { Count: > 0 } && roleCodes.Count > 0)
                {
                    foreach (var perm in rolePermissions)
                        table.Rows.Add(perm, roleCodes[0]);
                }
            }
            else if (sql.Contains("WHERE PermissionCode LIKE N'Platform.%'", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("PermissionCode", typeof(string));
                foreach (var perm in PlatformPermissions.All.Where(p => !p.StartsWith("Platform.Tenants.", StringComparison.OrdinalIgnoreCase)))
                    table.Rows.Add(perm);
            }
            else if (sql.Contains("FROM TenantModules tm", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("ModuleCode", typeof(string));
                foreach (var m in enabledModules)
                    table.Rows.Add(m);
            }
            else if (sql.Contains("FROM TenantFeatures", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("FeatureKey", typeof(string));
                foreach (var f in enabledFeatures)
                    table.Rows.Add(f);
            }
            else if (sql.Contains("SELECT PermissionCode, ModuleKey, FeatureKey FROM Permissions", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("PermissionCode", typeof(string));
                table.Columns.Add("ModuleKey", typeof(string));
                table.Columns.Add("FeatureKey", typeof(string));
                if (permissionGates is not null)
                {
                    foreach (var kv in permissionGates)
                        table.Rows.Add(kv.Key, (object?)kv.Value.ModuleKey ?? DBNull.Value, (object?)kv.Value.FeatureKey ?? DBNull.Value);
                }
            }
            else
            {
                table.Columns.Add("PermissionCode", typeof(string));
            }

            return table.CreateDataReader();
        }
    }

    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull]
        public override string ParameterName { get; set; } = "";
        public override int Size { get; set; }
        [AllowNull]
        public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<object> _items = [];
        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;
        public override int Add(object value) { _items.Add(value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (var v in values) _items.Add(v!); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains(value);
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf(value);
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) => _items.Insert(index, value);
        public override void Remove(object value) => _items.Remove(value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => (DbParameter)_items[index];
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotSupportedException();
    }
}
