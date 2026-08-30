using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Website CMS tables, seed content (from current marketing copy), permissions, and PlatformMenus.
/// </summary>
public static class WebsiteCmsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await CreateTablesAsync(connection, cancellationToken);
        await SeedPermissionsAndMenusAsync(connection, cancellationToken);
        await SeedContentAsync(connection, cancellationToken);
        logger.LogInformation("WebsiteCmsMigration applied successfully.");
    }

    private static async Task CreateTablesAsync(IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'WebsiteSettings', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsiteSettings (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsiteSettings_TenantId DEFAULT 1,
                    SiteName NVARCHAR(120) NOT NULL,
                    LogoUrl NVARCHAR(500) NULL,
                    FaviconUrl NVARCHAR(500) NULL,
                    SupportEmail NVARCHAR(200) NULL,
                    SalesEmail NVARCHAR(200) NULL,
                    PrivacyEmail NVARCHAR(200) NULL,
                    Phone NVARCHAR(60) NULL,
                    Address NVARCHAR(400) NULL,
                    LinkedInUrl NVARCHAR(300) NULL,
                    FacebookUrl NVARCHAR(300) NULL,
                    XUrl NVARCHAR(300) NULL,
                    YouTubeUrl NVARCHAR(300) NULL,
                    DefaultMetaTitle NVARCHAR(200) NULL,
                    DefaultMetaDescription NVARCHAR(500) NULL,
                    AnalyticsId NVARCHAR(80) NULL,
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteSettings_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE UNIQUE INDEX IX_WebsiteSettings_Tenant ON WebsiteSettings(TenantId);
            END

            IF OBJECT_ID(N'WebsitePages', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsitePages (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsitePages_TenantId DEFAULT 1,
                    Slug NVARCHAR(120) NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Description NVARCHAR(1000) NULL,
                    MetaTitle NVARCHAR(200) NULL,
                    MetaDescription NVARCHAR(500) NULL,
                    OgImage NVARCHAR(500) NULL,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WebsitePages_Status DEFAULT N'Draft',
                    PublishedAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsitePages_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsitePages_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE UNIQUE INDEX IX_WebsitePages_Tenant_Slug ON WebsitePages(TenantId, Slug);
            END

            IF OBJECT_ID(N'WebsiteSections', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsiteSections (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsiteSections_TenantId DEFAULT 1,
                    PageId INT NOT NULL,
                    SectionType NVARCHAR(80) NOT NULL,
                    Title NVARCHAR(300) NULL,
                    Subtitle NVARCHAR(300) NULL,
                    Content NVARCHAR(MAX) NULL,
                    ImageUrl NVARCHAR(500) NULL,
                    ButtonText NVARCHAR(120) NULL,
                    ButtonUrl NVARCHAR(300) NULL,
                    SecondaryButtonText NVARCHAR(120) NULL,
                    SecondaryButtonUrl NVARCHAR(300) NULL,
                    DisplayOrder INT NOT NULL CONSTRAINT DF_WebsiteSections_Order DEFAULT 0,
                    IsActive BIT NOT NULL CONSTRAINT DF_WebsiteSections_Active DEFAULT 1,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WebsiteSections_Status DEFAULT N'Draft',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteSections_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteSections_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_WebsiteSections_Page FOREIGN KEY (PageId) REFERENCES WebsitePages(Id)
                );
                CREATE INDEX IX_WebsiteSections_Page ON WebsiteSections(PageId, DisplayOrder);
            END

            IF OBJECT_ID(N'WebsiteFeatures', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsiteFeatures (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsiteFeatures_TenantId DEFAULT 1,
                    Title NVARCHAR(160) NOT NULL,
                    Description NVARCHAR(1000) NULL,
                    IconKey NVARCHAR(80) NULL,
                    ImageUrl NVARCHAR(500) NULL,
                    LinkUrl NVARCHAR(300) NULL,
                    DisplayOrder INT NOT NULL CONSTRAINT DF_WebsiteFeatures_Order DEFAULT 0,
                    IsActive BIT NOT NULL CONSTRAINT DF_WebsiteFeatures_Active DEFAULT 1,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WebsiteFeatures_Status DEFAULT N'Draft',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteFeatures_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteFeatures_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
            END

            IF OBJECT_ID(N'WebsiteMedia', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsiteMedia (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsiteMedia_TenantId DEFAULT 1,
                    FileName NVARCHAR(260) NOT NULL,
                    FileUrl NVARCHAR(500) NOT NULL,
                    StorageKey NVARCHAR(500) NULL,
                    FileType NVARCHAR(80) NULL,
                    AltText NVARCHAR(200) NULL,
                    SizeBytes BIGINT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteMedia_CreatedAt DEFAULT SYSUTCDATETIME()
                );
            END

            IF OBJECT_ID(N'WebsiteLegalDocuments', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsiteLegalDocuments (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsiteLegal_TenantId DEFAULT 1,
                    DocType NVARCHAR(40) NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    Version NVARCHAR(40) NULL,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WebsiteLegal_Status DEFAULT N'Draft',
                    PublishedAt DATETIME2 NULL,
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteLegal_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE UNIQUE INDEX IX_WebsiteLegal_Tenant_Type ON WebsiteLegalDocuments(TenantId, DocType);
            END

            IF OBJECT_ID(N'WebsiteContactRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsiteContactRequests (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsiteContact_TenantId DEFAULT 1,
                    FirstName NVARCHAR(80) NOT NULL,
                    LastName NVARCHAR(80) NOT NULL,
                    Company NVARCHAR(160) NOT NULL,
                    Email NVARCHAR(200) NOT NULL,
                    Phone NVARCHAR(40) NULL,
                    Country NVARCHAR(80) NULL,
                    FleetSize NVARCHAR(80) NULL,
                    InterestedIn NVARCHAR(120) NULL,
                    Message NVARCHAR(MAX) NOT NULL,
                    Status NVARCHAR(40) NOT NULL CONSTRAINT DF_WebsiteContact_Status DEFAULT N'New',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteContact_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteContact_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE INDEX IX_WebsiteContact_Status ON WebsiteContactRequests(TenantId, Status, CreatedAt DESC);
            END

            IF OBJECT_ID(N'WebsiteDemoRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE WebsiteDemoRequests (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WebsiteDemo_TenantId DEFAULT 1,
                    Name NVARCHAR(120) NOT NULL,
                    Company NVARCHAR(160) NOT NULL,
                    Email NVARCHAR(200) NOT NULL,
                    Phone NVARCHAR(40) NULL,
                    Country NVARCHAR(80) NULL,
                    VehicleCount NVARCHAR(40) NULL,
                    CurrentGpsProvider NVARCHAR(120) NULL,
                    InterestedProduct NVARCHAR(120) NULL,
                    Message NVARCHAR(MAX) NULL,
                    Status NVARCHAR(40) NOT NULL CONSTRAINT DF_WebsiteDemo_Status DEFAULT N'New',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteDemo_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WebsiteDemo_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE INDEX IX_WebsiteDemo_Status ON WebsiteDemoRequests(TenantId, Status, CreatedAt DESC);
            END
            """, cancellationToken: ct));
    }

    private static async Task SeedPermissionsAndMenusAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: ct)) != 1)
            return;

        var perms = new (string Code, string Desc)[]
        {
            (WebsitePermissions.View, "View Website Management"),
            (WebsitePermissions.Edit, "Edit website content"),
            (WebsitePermissions.Publish, "Publish website content"),
            (WebsitePermissions.Media, "Manage website media library"),
            (WebsitePermissions.ContactRequests, "Manage website contact requests"),
            (WebsitePermissions.DemoRequests, "Manage website demo requests"),
            (WebsitePermissions.Legal, "Edit website legal documents"),
            (WebsitePermissions.Settings, "Manage website settings"),
        };

        foreach (var (code, desc) in perms)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'Website', @Code, @Desc);
                """, new { Code = code, Desc = desc }, cancellationToken: ct));
        }

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", WebsitePermissions.All, ct);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", WebsitePermissions.All, ct);

        // Ensure Website module exists for menus
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformModules')
            AND NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'website')
            BEGIN
                INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
                VALUES (N'Website', N'website', N'language', 90, 1);
            END
            """, cancellationToken: ct));

        var menus = new (string Name, string Route, string Icon, string Perm, int Sort, string Display)[]
        {
            ("Website", "/website", "language", WebsitePermissions.View, 10, "Website Dashboard"),
            ("Home Page", "/website/home", "home", WebsitePermissions.Edit, 20, "Home Page"),
            ("Features", "/website/features", "apps", WebsitePermissions.Edit, 30, "Features"),
            ("Pages", "/website/pages", "article", WebsitePermissions.Edit, 40, "Pages"),
            ("Contact Requests", "/website/contact-requests", "mail", WebsitePermissions.ContactRequests, 50, "Contact Requests"),
            ("Demo Requests", "/website/demo-requests", "handshake", WebsitePermissions.DemoRequests, 60, "Demo Requests"),
            ("Media Library", "/website/media", "photo_library", WebsitePermissions.Media, 70, "Media Library"),
            ("Legal", "/website/legal", "gavel", WebsitePermissions.Legal, 80, "Legal"),
            ("Website Settings", "/website/settings", "tune", WebsitePermissions.Settings, 90, "Website Settings"),
        };

        foreach (var m in menus)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'website')
                AND NOT EXISTS (SELECT 1 FROM PlatformMenus WHERE Route = @Route)
                BEGIN
                    INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                        DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                    SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                           @Display, N'Website CMS', N'Website', 1, N'website', N'website', 0, SYSUTCDATETIME()
                    FROM PlatformModules mod WHERE mod.ModuleKey = N'website';
                END
                """, new { m.Name, m.Route, m.Icon, m.Perm, m.Sort, m.Display }, cancellationToken: ct));
        }
    }

    private static async Task SeedContentAsync(IDbConnection connection, CancellationToken ct)
    {
        var hasSettings = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM WebsiteSettings WHERE TenantId = 1) THEN 1 ELSE 0 END",
            cancellationToken: ct));
        if (hasSettings == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WebsiteSettings (TenantId, SiteName, LogoUrl, SupportEmail, SalesEmail, PrivacyEmail,
                    DefaultMetaTitle, DefaultMetaDescription)
                VALUES (1, N'SheikhGo', N'/brand/sheikhgo-logo.png',
                    N'info@sheikhgo.com', N'info@sheikhgo.com', N'info@sheikhgo.com',
                    N'SheikhGo | Intelligent Fleet & Travel Management',
                    N'SheikhGo helps businesses manage vehicles, drivers, trips, GPS tracking, maintenance and operations.');
                """, cancellationToken: ct));
        }

        async Task<int> EnsurePage(string slug, string title, string? desc, string? metaTitle, string? metaDesc)
        {
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Id FROM WebsitePages WHERE TenantId = 1 AND Slug = @Slug",
                new {Slug = slug }, cancellationToken: ct));
            if (id is > 0) return id.Value;

            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO WebsitePages (TenantId, Slug, Title, Description, MetaTitle, MetaDescription, Status, PublishedAt)
                OUTPUT INSERTED.Id
                VALUES (1, @Slug, @Title, @Desc, @MetaTitle, @MetaDesc, N'Published', SYSUTCDATETIME());
                """, new { Slug = slug, Title = title, Desc = desc, MetaTitle = metaTitle, MetaDesc = metaDesc },
                cancellationToken: ct));
        }

        var homeId = await EnsurePage("home", "Home",
            "SheikhGo marketing home",
            "Intelligent Fleet & Travel Management | SheikhGo",
            "Manage vehicles, drivers, trips, GPS tracking, maintenance and operations from one platform.");
        await EnsurePage("fleet-management", "Fleet Management",
            "Operate vehicles, drivers, assignments, maintenance, fuel and live tracking.",
            "Fleet Management Software | SheikhGo",
            "Manage vehicles, drivers, trips, GPS tracking, maintenance and fleet operations with SheikhGo.");
        await EnsurePage("gps-tracking", "GPS Tracking",
            "Real-time GPS fleet tracking with human-readable addresses.",
            "GPS Tracking | SheikhGo",
            "Track vehicles in real time with live maps, history playback, geofences and alerts.");
        await EnsurePage("features", "Features",
            "Explore SheikhGo platform capabilities.",
            "Features | SheikhGo",
            "Fleet, GPS, trips, maintenance, fuel, alerts, analytics and AI.");
        await EnsurePage("about", "About",
            "About Sheikh Travel Group and SheikhGo.",
            "About | SheikhGo",
            "Intelligent fleet and travel management from Sheikh Travel Group.");

        var sectionCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM WebsiteSections WHERE PageId = @Id", new { Id = homeId }, cancellationToken: ct));
        if (sectionCount == 0)
        {
            var homeSections = new (string Type, string Title, string? Sub, string? Content, string? Btn, string? BtnUrl, string? B2, string? B2Url, int Order)[]
            {
                ("Hero", "Move Smarter. Travel Further.", "Intelligent Fleet & Travel Management Platform",
                    "SheikhGo is an intelligent fleet and travel management platform that helps businesses manage vehicles, drivers, trips, GPS tracking, maintenance, fuel and operations from one powerful platform.",
                    "Request a Demo", "/request-demo", "Explore Fleet Management", "/fleet-management", 10),
                ("Trust", "Built for modern fleets", null, "24/7 GPS monitoring · One platform · Real-time · Secure RBAC", null, null, null, null, 20),
                ("Features", "Everything your fleet needs", "What SheikhGo does",
                    "Six pillars that cover day-to-day fleet operations without switching tools.",
                    null, null, null, null, 30),
                ("FleetTracking", "Monitor your entire fleet in real time", "Live fleet tracking",
                    "Real-time vehicle location, online/offline status, speed monitoring, GPS history, trip playback, geofencing, alerts and device commands.",
                    "Explore Fleet Tracking", "/gps-tracking", null, null, 40),
                ("Dashboard", "Everything your fleet needs in one dashboard", "Fleet dashboard",
                    "See vehicles, drivers, online status, active alerts, trips, maintenance and fuel at a glance.",
                    "Explore Fleet Management", "/fleet-management", null, null, 50),
                ("TripPlayback", "Understand every journey", "Trip playback",
                    "Replay vehicle journeys with detailed GPS history — route, stops, parking, speed and distance.",
                    "Explore Trip Tracking", "/gps-tracking", null, null, 60),
                ("Alerts", "Stay ahead of fleet problems", "Alerts",
                    "Receive actionable alerts you can acknowledge and resolve with role-based permissions.",
                    null, null, null, null, 70),
                ("Reports", "More than GPS tracking", "Reports & analytics",
                    "Operational analytics across fleet, trips, utilization, fuel and maintenance.",
                    "Explore Analytics", "/features", null, null, 80),
                ("AI", "Turn fleet data into actionable insights", "SheikhGo AI",
                    "Early AI capabilities help operators ask operational questions across fleet data. Capabilities continue to expand.",
                    "Explore SheikhGo AI", "/features", null, null, 90),
                ("Integrations", "Built on a real GPS stack", "Integrations",
                    "Jimi GPS → Traccar → SheikhGo GPS Engine → ERP / Mobile. Also supported: maps, Azure hosting, email and SMS.",
                    null, null, null, null, 100),
                ("Security", "Enterprise-ready controls", "Security",
                    "HTTPS/TLS, authentication & JWT, role-based access, tenant isolation, permission management, audit logging.",
                    null, null, null, null, 110),
                ("CTA", "Ready to manage your fleet smarter?", null,
                    "Bring vehicles, drivers, trips, GPS tracking and operations together with SheikhGo.",
                    "Request a Demo", "/request-demo", "Contact Sales", "/contact", 120),
            };

            foreach (var s in homeSections)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO WebsiteSections (TenantId, PageId, SectionType, Title, Subtitle, Content,
                        ButtonText, ButtonUrl, SecondaryButtonText, SecondaryButtonUrl, DisplayOrder, IsActive, Status)
                    VALUES (1, @PageId, @Type, @Title, @Sub, @Content, @Btn, @BtnUrl, @B2, @B2Url, @Order, 1, N'Published');
                    """, new
                    {
                        PageId = homeId,
                        s.Type,
                        s.Title,
                        s.Sub,
                        s.Content,
                        s.Btn,
                        s.BtnUrl,
                        s.B2,
                        s.B2Url,
                        s.Order
                    }, cancellationToken: ct));
            }
        }

        var featureCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM WebsiteFeatures WHERE TenantId = 1", cancellationToken: ct));
        if (featureCount == 0)
        {
            var features = new (string Title, string Desc, string Icon, string Link, int Order)[]
            {
                ("Fleet Management", "Manage vehicles, assignments, status and operational information from one place.", "directions_car", "/fleet-management", 10),
                ("GPS Tracking", "Track vehicles in real time with Jimi/Traccar-connected devices and live maps.", "my_location", "/gps-tracking", 20),
                ("Driver Management", "Manage drivers, assignments, availability and performance.", "badge", "/features", 30),
                ("Trip Management", "Create, monitor and complete trips with detailed history and playback.", "route", "/features", 40),
                ("Maintenance", "Track service schedules, work orders and maintenance history.", "build", "/features", 50),
                ("Fuel Management", "Monitor fuel usage, costs and vehicle fuel performance.", "local_gas_station", "/features", 60),
                ("Alerts", "Offline, overspeed, geofence and maintenance alerts.", "notifications_active", "/gps-tracking", 70),
                ("Reports & Analytics", "Utilization, distance, speed, fuel and ops analytics.", "analytics", "/features", 80),
                ("SheikhGo AI", "Ask operational questions across fleet data.", "smart_toy", "/features", 90),
            };
            foreach (var f in features)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO WebsiteFeatures (TenantId, Title, Description, IconKey, LinkUrl, DisplayOrder, IsActive, Status)
                    VALUES (1, @Title, @Desc, @Icon, @Link, @Order, 1, N'Published');
                    """, new { f.Title, f.Desc, f.Icon, f.Link, f.Order }, cancellationToken: ct));
            }
        }

        var legalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM WebsiteLegalDocuments WHERE TenantId = 1", cancellationToken: ct));
        if (legalCount == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WebsiteLegalDocuments (TenantId, DocType, Title, Content, Version, Status, PublishedAt)
                VALUES
                (1, N'Privacy', N'Privacy Policy',
                 N'<p>This Privacy Policy explains how SheikhGo collects and uses information when you use the SheikhGo website, ERP and related mobile apps. This is a product draft — have legal counsel review before binding publication.</p><h2>Information we collect</h2><p>Account, vehicle, driver, location/GPS, technical and website form data as described on the public privacy page.</p><h2>Contact</h2><p>info@sheikhgo.com</p>',
                 N'1.0', N'Published', SYSUTCDATETIME()),
                (1, N'Terms', N'Terms & Conditions',
                 N'<p>By accessing or using SheikhGo you agree to these Terms. GPS accuracy depends on device, network and environmental conditions. This is a product draft for counsel review.</p>',
                 N'1.0', N'Published', SYSUTCDATETIME()),
                (1, N'Cookie', N'Cookie Policy',
                 N'<p>SheikhGo uses essential and session cookies to operate the website and authenticated applications. Non-essential analytics cookies are not currently loaded on the marketing site.</p>',
                 N'1.0', N'Published', SYSUTCDATETIME());
                """, cancellationToken: ct));
        }
    }
}
