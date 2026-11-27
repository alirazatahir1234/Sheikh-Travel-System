using System.Text.RegularExpressions;
using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Infrastructure;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;

namespace SheikhTravelSystem.Tests.TravelNexus.LiveSql;

[CollectionDefinition(Name)]
public sealed class TravelNexusLiveSqlCollection : ICollectionFixture<TravelNexusLiveDbFixture>
{
    public const string Name = "TravelNexusLiveSql";
}

/// <summary>One seeded company: its tenant row, head-office branch and administrator user.</summary>
public sealed record TenantSeed(int TenantId, int UserId, int BranchId, string Slug);

/// <summary>
/// Throwaway database for the TravelNexus end-to-end tests. On the SQL Server named by
/// <see cref="TravelLiveSqlFactAttribute.ConnectionEnv"/> it creates <c>SheikhGo_TravelNexus_E2E_&lt;stamp&gt;</c>, runs
/// <c>Backend/Database/CompleteSchema.sql</c> and then every registered migration through the real
/// <see cref="DatabaseMigrationRunner"/>, seeds two companies and builds the production dependency-injection graph
/// (<c>AddApplication</c> + <c>AddInfrastructure</c>) against that database, so tests drive the real MediatR pipeline,
/// handlers and Dapper repositories. The database is dropped when the collection finishes unless
/// <see cref="TravelLiveSqlFactAttribute.KeepDbEnv"/> is set. The connection string is never logged.
/// </summary>
public sealed class TravelNexusLiveDbFixture : IAsyncLifetime
{
    public const string TravelAgency = TenantModuleCatalog.TravelAgencyModuleCode;

    private string? _masterConnection;
    private string? _databaseName;
    private string? _root;

    public bool Available { get; private set; }
    public string DatabaseConnection { get; private set; } = string.Empty;
    /// <summary>Connection factory bound to the throwaway database (for running a migration or a repository directly).</summary>
    public IDbConnectionFactory Factory => new LiveFactory(DatabaseConnection);
    public ServiceProvider Provider { get; private set; } = null!;
    public Mock<INotificationService> Notifications { get; } = new();

    /// <summary>Company A — TravelNexus enabled, reporting currency PKR.</summary>
    public TenantSeed A { get; private set; } = null!;
    /// <summary>Company B — TravelNexus enabled; must never see company A's rows.</summary>
    public TenantSeed B { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(TravelLiveSqlFactAttribute.ConnectionEnv);
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (TravelLiveSqlFactAttribute.Required)
                throw new InvalidOperationException($"{TravelLiveSqlFactAttribute.RequireEnv}=1 but {TravelLiveSqlFactAttribute.ConnectionEnv} is not set.");
            return; // every [TravelLiveSqlFact] is skipped; nothing to build
        }

        var builder = new SqlConnectionStringBuilder(configured);
        if (!string.IsNullOrEmpty(builder.InitialCatalog) && !builder.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{TravelLiveSqlFactAttribute.ConnectionEnv} must point at master (or no database): the fixture creates and drops its own database and refuses to touch an application database.");
        builder.InitialCatalog = "master";
        _masterConnection = builder.ConnectionString;

        _databaseName = $"SheikhGo_TravelNexus_E2E_{DateTime.UtcNow:yyyyMMddHHmmss}_{Environment.ProcessId}";
        _root = Path.Combine(Path.GetTempPath(), _databaseName);
        Directory.CreateDirectory(_root);

        await using (var master = new SqlConnection(_masterConnection))
        {
            await master.OpenAsync();
            await master.ExecuteAsync($"CREATE DATABASE [{_databaseName}]");
        }

        builder.InitialCatalog = _databaseName;
        DatabaseConnection = builder.ConnectionString;

        await ApplyBaseSchemaAsync();
        await ApplyMigrationsAsync();

        Notifications.Setup(n => n.CreateAndDispatchAsync(It.IsAny<NotificationCreateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        Provider = BuildServices(DatabaseConnection, _root, Notifications.Object);

        A = await CreateTenantAsync("e2e-company-a", enableTravel: true);
        B = await CreateTenantAsync("e2e-company-b", enableTravel: true);
        Available = true;
    }

    public async Task DisposeAsync()
    {
        if (Provider is not null) await Provider.DisposeAsync();
        if (_masterConnection is null || _databaseName is null) return;

        if (Environment.GetEnvironmentVariable(TravelLiveSqlFactAttribute.KeepDbEnv) == "1") return;
        try
        {
            await using var master = new SqlConnection(_masterConnection);
            await master.OpenAsync();
            await master.ExecuteAsync($"""
                IF DB_ID(N'{_databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_databaseName}];
                END
                """);
            if (_root is not null && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best effort: a leftover throwaway database is harmless and named by its timestamp
        }
    }

    // ── Schema ──────────────────────────────────────────────────────────────

    private async Task ApplyBaseSchemaAsync()
    {
        var path = Path.Combine(FindBackendRoot(), "Database", "CompleteSchema.sql");
        var script = await File.ReadAllTextAsync(path);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        await using var conn = new SqlConnection(DatabaseConnection);
        await conn.OpenAsync();
        foreach (var raw in batches)
        {
            // The script's own CREATE DATABASE / USE header targets a fixed name (after a comment banner); this fixture
            // owns the database, so those statements are dropped wherever they sit in a batch.
            var sql = Regex.Replace(raw, @"^\s*(CREATE\s+DATABASE|USE)\s+[^\r\n]*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase).Trim();
            if (sql.Length == 0 || Regex.IsMatch(sql, @"^(\s*--[^\n]*\n?)*\s*$")) continue;
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task ApplyMigrationsAsync()
    {
        var runner = new DatabaseMigrationRunner(new LiveFactory(DatabaseConnection), NullLogger<DatabaseMigrationRunner>.Instance);
        var result = await runner.ApplyPendingAsync("travelnexus-e2e");
        if (result.FailedMigration is not null)
            throw new InvalidOperationException($"Migration {result.FailedMigration} failed on an empty database: {result.ErrorMessage}");
        if (result.AppliedCount != DatabaseMigrationRegistry.All.Count)
            throw new InvalidOperationException($"Expected {DatabaseMigrationRegistry.All.Count} migrations applied, got {result.AppliedCount}.");
    }

    // ── Dependency injection ────────────────────────────────────────────────

    /// <summary>
    /// The production graph (<c>AddApplication</c> + <c>AddInfrastructure</c>) with request-bound services replaced by
    /// test doubles: tenant and user come from <see cref="E2ESessionState"/>, branch scope is company-wide, notifications
    /// are recorded on a mock, and files go under <paramref name="root"/>. Hosted services are registered but never started.
    /// </summary>
    public static ServiceProvider BuildServices(string databaseConnection, string root, INotificationService notifications)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = databaseConnection,
            ["FileStorage:Provider"] = "Local",
            ["FileStorage:RootPath"] = Path.Combine(root, "uploads"),
            ["TravelDocuments:LocalRoot"] = Path.Combine(root, "private-storage", "travel-documents"),
            ["TravelNexus:Reminders:Enabled"] = "false",
            ["Localization:DefaultTimeZone"] = "Asia/Karachi",
            ["JwtSettings:Secret"] = new string('e', 64),
            ["JwtSettings:Issuer"] = "e2e",
            ["JwtSettings:Audience"] = "e2e",
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSignalR();
        services.AddDataProtection();
        var environment = new E2EHostEnvironment(root);
        services.AddSingleton<IHostEnvironment>(environment);

        services.AddApplication();
        services.AddInfrastructure(configuration);

        // Request-bound services → session state (last registration wins).
        services.AddScoped<E2ESessionState>();
        services.AddScoped<ITenantContext>(sp =>
        {
            var state = sp.GetRequiredService<E2ESessionState>();
            var context = new TenantContext();
            if (state.TenantId > 0) context.SetTenant(state.TenantId, state.Slug);
            return context;
        });
        services.AddScoped<ICurrentUserService, E2ECurrentUser>();
        services.AddScoped<IPlatformScope, E2EPlatformScope>();
        services.AddScoped<ITravelBranchScopeResolver, CompanyWideScopeResolver>();
        services.AddScoped(_ => notifications);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    // ── Companies ───────────────────────────────────────────────────────────

    /// <summary>Seeds a company (tenant, branch, admin user, TENANT_ADMIN + OWNER roles) and optionally enables TravelNexus through the real handler.</summary>
    public async Task<TenantSeed> CreateTenantAsync(string slug, bool enableTravel)
    {
        int tenantId, branchId, userId;
        await using (var conn = new SqlConnection(DatabaseConnection))
        {
            await conn.OpenAsync();
            tenantId = await conn.ExecuteScalarAsync<int>("""
                INSERT INTO Tenants (Name, Slug, IsActive, EnabledModulesJson, CreatedAt)
                OUTPUT INSERTED.Id VALUES (@Name, @Slug, 1, N'[]', GETUTCDATE());
                """, new { Name = $"E2E {slug}", Slug = slug });
            branchId = await conn.ExecuteScalarAsync<int>("""
                INSERT INTO Branches (TenantId, Name, IsActive) OUTPUT INSERTED.Id VALUES (@TenantId, N'Head office', 1);
                """, new { TenantId = tenantId });
            userId = await conn.ExecuteScalarAsync<int>("""
                INSERT INTO Users (TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, BranchId)
                OUTPUT INSERTED.Id VALUES (@TenantId, @Name, @Email, N'e2e-not-a-real-hash', N'0300000000', 1, 1, @BranchId);
                """, new { TenantId = tenantId, Name = $"{slug} admin", Email = $"{slug}-admin@e2e.local", BranchId = branchId });
            await conn.ExecuteAsync("""
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                VALUES (@TenantId, N'Company Admin', N'TENANT_ADMIN', 1, 1, GETUTCDATE()), (@TenantId, N'Owner', N'OWNER', 1, 1, GETUTCDATE());
                INSERT INTO UserRoles (UserId, RoleId)
                SELECT @UserId, r.Id FROM Roles r WHERE r.TenantId = @TenantId AND r.Code = N'TENANT_ADMIN';
                """, new { TenantId = tenantId, UserId = userId });
        }

        var seed = new TenantSeed(tenantId, userId, branchId, slug);
        if (enableTravel)
        {
            using var session = Open(seed);
            var enabled = await session.Send(new SetTenantModulesCommand(tenantId, [TravelAgency]));
            if (!enabled.Success) throw new InvalidOperationException($"Could not enable TravelNexus for {slug}: {enabled.Message}");
            var settings = await session.Send(new UpdateTravelSettingsCommand(
                new UpdateTravelSettingsDto("PKR", "ENQ", "VSA", "INV", 180, ReportingCurrencyCode: "PKR", DefaultPaymentTermsDays: 7)));
            if (!settings.Success) throw new InvalidOperationException($"Could not save travel settings for {slug}: {settings.Message}");
        }
        return seed;
    }

    /// <summary>A request scope acting as the given company's administrator.</summary>
    public TenantSession Open(TenantSeed tenant) => Open(tenant.TenantId, tenant.UserId, tenant.Slug);

    public TenantSession Open(int tenantId, int userId, string? slug = null)
    {
        var scope = Provider.CreateScope();
        var state = scope.ServiceProvider.GetRequiredService<E2ESessionState>();
        state.TenantId = tenantId;
        state.UserId = userId;
        state.Slug = slug;
        return new TenantSession(scope);
    }

    public SqlConnection OpenDb()
    {
        var conn = new SqlConnection(DatabaseConnection);
        conn.Open();
        return conn;
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(Path.Combine(backend, "SheikhTravelSystem.Infrastructure"))) return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend/ root.");
    }

    private sealed class LiveFactory(string connectionString) : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => new SqlConnection(connectionString);
    }

    private sealed class E2EHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SheikhTravelSystem.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}

/// <summary>One request scope bound to a company and user; <see cref="Send{T}"/> goes through the full MediatR pipeline.</summary>
public sealed class TenantSession(IServiceScope scope) : IDisposable
{
    public IServiceProvider Services => scope.ServiceProvider;
    private IMediator Mediator => scope.ServiceProvider.GetRequiredService<IMediator>();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request) => Mediator.Send(request);

    /// <summary>Sends and asserts the envelope reports success, returning its data.</summary>
    public async Task<T> Ok<T>(IRequest<ApiResponse<T>> request)
    {
        var response = await Mediator.Send(request);
        if (!response.Success)
            throw new Xunit.Sdk.XunitException($"{request.GetType().Name} failed: {response.Message}");
        return response.Data!;
    }

    public T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose() => scope.Dispose();
}

/// <summary>Per-scope tenant / user, in place of the JWT the API would have read.</summary>
public sealed class E2ESessionState
{
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string? Slug { get; set; }
}

public sealed class E2ECurrentUser(E2ESessionState state) : ICurrentUserService
{
    public int? UserId => state.UserId > 0 ? state.UserId : null;
    public string? Role => "TENANT_ADMIN";
    public int? DriverId => null;
    public Guid? SessionId => null;
    public bool HasPermission(string permission) => true;
    public bool IsPlatformSuperAdmin => false;
    public IReadOnlyList<string> Roles => ["TENANT_ADMIN"];
}

public sealed class E2EPlatformScope(E2ESessionState state) : IPlatformScope
{
    public int TenantId => state.TenantId;
    public bool HasPermission(string permission) => true;
    public void EnsureTenantAccess(int tenantId) { }
    public int ResolveTenantId(int? requestedTenantId) => requestedTenantId ?? state.TenantId;
}

public sealed class CompanyWideScopeResolver : ITravelBranchScopeResolver
{
    public Task<TravelBranchScope> ResolveAsync(int tenantId, CancellationToken cancellationToken) => Task.FromResult(TravelBranchScope.CompanyWide);
}
