using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Evolves WhatsApp inbox tables to the audited multi-account domain foundation
/// (Account / Conversation / Message). No access-token columns.
/// </summary>
public static class WhatsAppDomainFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await EnsureTargetTablesAsync(connection, cancellationToken);
        await EvolveAccountsAsync(connection, cancellationToken);
        await EvolveConversationsAsync(connection, cancellationToken);
        await EvolveMessagesAsync(connection, cancellationToken);
        await SeedAccountMetadataAsync(connection, cancellationToken);
        logger.LogInformation("WhatsAppDomainFoundationMigration applied successfully.");
    }

    private static async Task EnsureTargetTablesAsync(IDbConnection connection, CancellationToken ct)
    {
        // Only create when missing. Use dynamic SQL so SQL Server does not bind CREATE INDEX
        // column lists to an already-existing legacy WhatsAppAccounts shape in the same batch.
        if (!await TableExistsAsync(connection, "WhatsAppAccounts", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                EXEC(N'
                CREATE TABLE WhatsAppAccounts (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WA_Acc_TenantId DEFAULT 1,
                    Code NVARCHAR(20) NOT NULL,
                    Name NVARCHAR(120) NOT NULL,
                    DisplayPhoneNumber NVARCHAR(32) NOT NULL,
                    PhoneNumberId NVARCHAR(64) NULL,
                    BusinessAccountId NVARCHAR(64) NULL,
                    CountryCode NVARCHAR(8) NULL,
                    Country NVARCHAR(8) NULL,
                    Purpose NVARCHAR(40) NOT NULL,
                    IsDefault BIT NOT NULL CONSTRAINT DF_WA_Acc_IsDefault DEFAULT 0,
                    IsActive BIT NOT NULL CONSTRAINT DF_WA_Acc_IsActive DEFAULT 1,
                    Status NVARCHAR(40) NOT NULL CONSTRAINT DF_WA_Acc_Status DEFAULT N''Active'',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WA_Acc_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WA_Acc_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE UNIQUE INDEX IX_WhatsAppAccounts_Tenant_Code ON WhatsAppAccounts(TenantId, Code);
                CREATE INDEX IX_WhatsAppAccounts_PhoneNumberId ON WhatsAppAccounts(PhoneNumberId) WHERE PhoneNumberId IS NOT NULL;
                CREATE INDEX IX_WhatsAppAccounts_Tenant_IsDefault ON WhatsAppAccounts(TenantId, IsDefault) WHERE IsDefault = 1;
                ');
                """, cancellationToken: ct));
        }

        if (!await TableExistsAsync(connection, "WhatsAppConversations", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                EXEC(N'
                CREATE TABLE WhatsAppConversations (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL,
                    AccountId INT NOT NULL,
                    CustomerPhoneNumber NVARCHAR(32) NOT NULL,
                    CustomerName NVARCHAR(200) NULL,
                    CustomerId INT NULL,
                    LeadId INT NULL,
                    AssignedUserId INT NULL,
                    Status NVARCHAR(40) NOT NULL CONSTRAINT DF_WA_Conv_Status DEFAULT N''Open'',
                    LastMessageAt DATETIME2 NULL,
                    LastIncomingMessageAt DATETIME2 NULL,
                    LastOutgoingMessageAt DATETIME2 NULL,
                    UnreadCount INT NOT NULL CONSTRAINT DF_WA_Conv_Unread DEFAULT 0,
                    IsBotEnabled BIT NOT NULL CONSTRAINT DF_WA_Conv_Bot DEFAULT 0,
                    CurrentBotState NVARCHAR(100) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WA_Conv_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WA_Conv_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_WhatsAppConversations_Account FOREIGN KEY (AccountId) REFERENCES WhatsAppAccounts(Id)
                );
                CREATE UNIQUE INDEX IX_WhatsAppConversations_Account_Phone
                    ON WhatsAppConversations(TenantId, AccountId, CustomerPhoneNumber);
                CREATE INDEX IX_WhatsAppConversations_LastMsg
                    ON WhatsAppConversations(TenantId, AccountId, LastMessageAt DESC);
                CREATE INDEX IX_WhatsAppConversations_Customer
                    ON WhatsAppConversations(TenantId, CustomerId) WHERE CustomerId IS NOT NULL;
                CREATE INDEX IX_WhatsAppConversations_Lead
                    ON WhatsAppConversations(TenantId, LeadId) WHERE LeadId IS NOT NULL;
                ');
                """, cancellationToken: ct));
        }

        if (!await TableExistsAsync(connection, "WhatsAppMessages", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                EXEC(N'
                CREATE TABLE WhatsAppMessages (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL,
                    ConversationId INT NOT NULL,
                    AccountId INT NOT NULL,
                    MessageId NVARCHAR(128) NULL,
                    Direction NVARCHAR(16) NOT NULL,
                    MessageType NVARCHAR(40) NOT NULL CONSTRAINT DF_WA_Msg_Type DEFAULT N''text'',
                    Text NVARCHAR(MAX) NULL,
                    MediaId NVARCHAR(128) NULL,
                    MediaUrl NVARCHAR(1000) NULL,
                    TemplateName NVARCHAR(200) NULL,
                    Status NVARCHAR(40) NOT NULL,
                    ErrorCode NVARCHAR(64) NULL,
                    ErrorMessage NVARCHAR(1000) NULL,
                    RawPayload NVARCHAR(MAX) NULL,
                    SentAt DATETIME2 NULL,
                    DeliveredAt DATETIME2 NULL,
                    ReadAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WA_Msg_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_WhatsAppMessages_Conversation FOREIGN KEY (ConversationId) REFERENCES WhatsAppConversations(Id),
                    CONSTRAINT FK_WhatsAppMessages_Account FOREIGN KEY (AccountId) REFERENCES WhatsAppAccounts(Id)
                );
                CREATE UNIQUE INDEX IX_WhatsAppMessages_MessageId ON WhatsAppMessages(MessageId) WHERE MessageId IS NOT NULL;
                CREATE INDEX IX_WhatsAppMessages_Conversation ON WhatsAppMessages(ConversationId, CreatedAt);
                CREATE INDEX IX_WhatsAppMessages_Account_Created ON WhatsAppMessages(TenantId, AccountId, CreatedAt);
                ');
                """, cancellationToken: ct));
        }

        if (!await TableExistsAsync(connection, "WhatsAppWebhookDeadLetters", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                CREATE TABLE WhatsAppWebhookDeadLetters (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Reason NVARCHAR(400) NOT NULL,
                    PayloadPreview NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WA_Dead_CreatedAt DEFAULT SYSUTCDATETIME()
                );
                """, cancellationToken: ct));
        }
    }

    private static async Task EvolveAccountsAsync(IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "Name", "NVARCHAR(120) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "DisplayPhoneNumber", "NVARCHAR(32) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "BusinessAccountId", "NVARCHAR(64) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "CountryCode", "NVARCHAR(8) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "Country", "NVARCHAR(8) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "IsDefault", "BIT NOT NULL CONSTRAINT DF_WA_Acc_IsDefault2 DEFAULT 0", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "Status", "NVARCHAR(40) NOT NULL CONSTRAINT DF_WA_Acc_Status2 DEFAULT N'Active'", ct);

        // Copy legacy columns when present.
        if (await ColumnExistsAsync(connection, "WhatsAppAccounts", "DisplayName", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppAccounts SET Name = DisplayName
                WHERE (Name IS NULL OR LTRIM(RTRIM(Name)) = '') AND DisplayName IS NOT NULL
                """, cancellationToken: ct));
        }

        if (await ColumnExistsAsync(connection, "WhatsAppAccounts", "E164Phone", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppAccounts SET DisplayPhoneNumber = E164Phone
                WHERE (DisplayPhoneNumber IS NULL OR LTRIM(RTRIM(DisplayPhoneNumber)) = '') AND E164Phone IS NOT NULL
                """, cancellationToken: ct));
        }

        if (await ColumnExistsAsync(connection, "WhatsAppAccounts", "WabaId", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppAccounts SET BusinessAccountId = WabaId
                WHERE BusinessAccountId IS NULL AND WabaId IS NOT NULL
                """, cancellationToken: ct));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAccounts SET Name = COALESCE(NULLIF(LTRIM(RTRIM(Name)), ''), Code, N'WhatsApp')
            WHERE Name IS NULL OR LTRIM(RTRIM(Name)) = '';
            UPDATE WhatsAppAccounts SET DisplayPhoneNumber = COALESCE(NULLIF(LTRIM(RTRIM(DisplayPhoneNumber)), ''), N'')
            WHERE DisplayPhoneNumber IS NULL;
            """, cancellationToken: ct));

        // Tighten NULLability only when safe (SQL Server: alter after fill).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'WhatsAppAccounts', N'Name') IS NOT NULL
               AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = N'WhatsAppAccounts' AND COLUMN_NAME = N'Name' AND IS_NULLABLE = N'YES')
                ALTER TABLE WhatsAppAccounts ALTER COLUMN Name NVARCHAR(120) NOT NULL;

            IF COL_LENGTH(N'WhatsAppAccounts', N'DisplayPhoneNumber') IS NOT NULL
               AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = N'WhatsAppAccounts' AND COLUMN_NAME = N'DisplayPhoneNumber' AND IS_NULLABLE = N'YES')
                ALTER TABLE WhatsAppAccounts ALTER COLUMN DisplayPhoneNumber NVARCHAR(32) NOT NULL;
            """, cancellationToken: ct));

        await EnsureIndexAsync(connection, "IX_WhatsAppAccounts_Tenant_IsDefault", """
            CREATE INDEX IX_WhatsAppAccounts_Tenant_IsDefault ON WhatsAppAccounts(TenantId, IsDefault) WHERE IsDefault = 1
            """, ct);

        await DropColumnIfExistsAsync(connection, "WhatsAppAccounts", "DisplayName", ct);
        await DropColumnIfExistsAsync(connection, "WhatsAppAccounts", "E164Phone", ct);
        await DropColumnIfExistsAsync(connection, "WhatsAppAccounts", "WabaId", ct);
    }

    private static async Task EvolveConversationsAsync(IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "CustomerPhoneNumber", "NVARCHAR(32) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "CustomerName", "NVARCHAR(200) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "CustomerId", "INT NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "LeadId", "INT NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "LastIncomingMessageAt", "DATETIME2 NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "LastOutgoingMessageAt", "DATETIME2 NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "IsBotEnabled", "BIT NOT NULL CONSTRAINT DF_WA_Conv_Bot2 DEFAULT 0", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversations", "CurrentBotState", "NVARCHAR(100) NULL", ct);

        // Backfill from WhatsAppContacts when ContactId still exists.
        if (await ColumnExistsAsync(connection, "WhatsAppConversations", "ContactId", ct)
            && await TableExistsAsync(connection, "WhatsAppContacts", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE c
                SET c.CustomerPhoneNumber = COALESCE(NULLIF(LTRIM(RTRIM(c.CustomerPhoneNumber)), ''), ct.PhoneE164),
                    c.CustomerName = COALESCE(c.CustomerName, ct.ProfileName),
                    c.CustomerId = COALESCE(c.CustomerId, ct.CustomerId)
                FROM WhatsAppConversations c
                INNER JOIN WhatsAppContacts ct ON ct.Id = c.ContactId
                """, cancellationToken: ct));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET CustomerPhoneNumber = COALESCE(NULLIF(LTRIM(RTRIM(CustomerPhoneNumber)), ''), N'unknown')
            WHERE CustomerPhoneNumber IS NULL OR LTRIM(RTRIM(CustomerPhoneNumber)) = '';
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'WhatsAppConversations', N'CustomerPhoneNumber') IS NOT NULL
               AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = N'WhatsAppConversations' AND COLUMN_NAME = N'CustomerPhoneNumber' AND IS_NULLABLE = N'YES')
                ALTER TABLE WhatsAppConversations ALTER COLUMN CustomerPhoneNumber NVARCHAR(32) NOT NULL;
            """, cancellationToken: ct));

        // Drop ContactId FK + column and old unique index.
        if (await ColumnExistsAsync(connection, "WhatsAppConversations", "ContactId", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                DECLARE @fk NVARCHAR(256);
                SELECT @fk = fk.name
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                WHERE fk.parent_object_id = OBJECT_ID(N'WhatsAppConversations')
                  AND c.name = N'ContactId';
                IF @fk IS NOT NULL
                    EXEC(N'ALTER TABLE WhatsAppConversations DROP CONSTRAINT [' + @fk + N']');

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WhatsAppConversations_Account_Contact' AND object_id = OBJECT_ID(N'WhatsAppConversations'))
                    DROP INDEX IX_WhatsAppConversations_Account_Contact ON WhatsAppConversations;

                ALTER TABLE WhatsAppConversations DROP COLUMN ContactId;
            """, cancellationToken: ct));
        }

        // Drop LastMessagePreview if present (preview derived from messages going forward).
        if (await ColumnExistsAsync(connection, "WhatsAppConversations", "LastMessagePreview", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                ALTER TABLE WhatsAppConversations DROP COLUMN LastMessagePreview;
            """, cancellationToken: ct));
        }

        await EnsureIndexAsync(connection, "IX_WhatsAppConversations_Account_Phone", """
            CREATE UNIQUE INDEX IX_WhatsAppConversations_Account_Phone
                ON WhatsAppConversations(TenantId, AccountId, CustomerPhoneNumber)
            """, ct);
        await EnsureIndexAsync(connection, "IX_WhatsAppConversations_Customer", """
            CREATE INDEX IX_WhatsAppConversations_Customer
                ON WhatsAppConversations(TenantId, CustomerId) WHERE CustomerId IS NOT NULL
            """, ct);
        await EnsureIndexAsync(connection, "IX_WhatsAppConversations_Lead", """
            CREATE INDEX IX_WhatsAppConversations_Lead
                ON WhatsAppConversations(TenantId, LeadId) WHERE LeadId IS NOT NULL
            """, ct);
    }

    private static async Task EvolveMessagesAsync(IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "AccountId", "INT NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "MessageId", "NVARCHAR(128) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "MessageType", "NVARCHAR(40) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "Text", "NVARCHAR(MAX) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "MediaUrl", "NVARCHAR(1000) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "TemplateName", "NVARCHAR(200) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "ErrorCode", "NVARCHAR(64) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "RawPayload", "NVARCHAR(MAX) NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "SentAt", "DATETIME2 NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "DeliveredAt", "DATETIME2 NULL", ct);
        await AddColumnIfMissingAsync(connection, "WhatsAppMessages", "ReadAt", "DATETIME2 NULL", ct);

        // Backfill AccountId from conversation.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE m SET m.AccountId = c.AccountId
            FROM WhatsAppMessages m
            INNER JOIN WhatsAppConversations c ON c.Id = m.ConversationId
            WHERE m.AccountId IS NULL
            """, cancellationToken: ct));

        // Orphan guard: if still null, skip NOT NULL alter (rare).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM WhatsAppMessages WHERE AccountId IS NULL)
            BEGIN
                DECLARE @fallback INT = (SELECT TOP 1 Id FROM WhatsAppAccounts ORDER BY Id);
                IF @fallback IS NOT NULL
                    UPDATE WhatsAppMessages SET AccountId = @fallback WHERE AccountId IS NULL;
            END
            """, cancellationToken: ct));

        if (await ColumnExistsAsync(connection, "WhatsAppMessages", "MetaMessageId", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppMessages SET MessageId = MetaMessageId
                WHERE MessageId IS NULL AND MetaMessageId IS NOT NULL
                """, cancellationToken: ct));
        }

        if (await ColumnExistsAsync(connection, "WhatsAppMessages", "Type", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppMessages SET MessageType = Type
                WHERE (MessageType IS NULL OR LTRIM(RTRIM(MessageType)) = '') AND Type IS NOT NULL
                """, cancellationToken: ct));
        }

        if (await ColumnExistsAsync(connection, "WhatsAppMessages", "Body", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppMessages SET Text = Body WHERE Text IS NULL AND Body IS NOT NULL
                """, cancellationToken: ct));
        }

        if (await ColumnExistsAsync(connection, "WhatsAppMessages", "RawJson", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppMessages SET RawPayload = RawJson WHERE RawPayload IS NULL AND RawJson IS NOT NULL
                """, cancellationToken: ct));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppMessages SET MessageType = COALESCE(NULLIF(LTRIM(RTRIM(MessageType)), ''), N'text')
            WHERE MessageType IS NULL OR LTRIM(RTRIM(MessageType)) = '';
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'WhatsAppMessages', N'AccountId') IS NOT NULL
               AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = N'WhatsAppMessages' AND COLUMN_NAME = N'AccountId' AND IS_NULLABLE = N'YES')
               AND NOT EXISTS (SELECT 1 FROM WhatsAppMessages WHERE AccountId IS NULL)
                ALTER TABLE WhatsAppMessages ALTER COLUMN AccountId INT NOT NULL;

            IF COL_LENGTH(N'WhatsAppMessages', N'MessageType') IS NOT NULL
               AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = N'WhatsAppMessages' AND COLUMN_NAME = N'MessageType' AND IS_NULLABLE = N'YES')
                ALTER TABLE WhatsAppMessages ALTER COLUMN MessageType NVARCHAR(40) NOT NULL;
            """, cancellationToken: ct));

        // Drop old unique index on MetaMessageId, then legacy columns.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WhatsAppMessages_MetaId' AND object_id = OBJECT_ID(N'WhatsAppMessages'))
                DROP INDEX IX_WhatsAppMessages_MetaId ON WhatsAppMessages;
            """, cancellationToken: ct));

        await DropColumnIfExistsAsync(connection, "WhatsAppMessages", "MetaMessageId", ct);
        await DropColumnIfExistsAsync(connection, "WhatsAppMessages", "Type", ct);
        await DropColumnIfExistsAsync(connection, "WhatsAppMessages", "Body", ct);
        await DropColumnIfExistsAsync(connection, "WhatsAppMessages", "RawJson", ct);

        // FK AccountId if missing.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_WhatsAppMessages_Account' AND parent_object_id = OBJECT_ID(N'WhatsAppMessages'))
               AND COL_LENGTH(N'WhatsAppMessages', N'AccountId') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM WhatsAppMessages WHERE AccountId IS NULL)
            BEGIN
                ALTER TABLE WhatsAppMessages
                ADD CONSTRAINT FK_WhatsAppMessages_Account FOREIGN KEY (AccountId) REFERENCES WhatsAppAccounts(Id);
            END
            """, cancellationToken: ct));

        await EnsureIndexAsync(connection, "IX_WhatsAppMessages_MessageId", """
            CREATE UNIQUE INDEX IX_WhatsAppMessages_MessageId ON WhatsAppMessages(MessageId) WHERE MessageId IS NOT NULL
            """, ct);
        await EnsureIndexAsync(connection, "IX_WhatsAppMessages_Account_Created", """
            CREATE INDEX IX_WhatsAppMessages_Account_Created ON WhatsAppMessages(TenantId, AccountId, CreatedAt)
            """, ct);
    }

    private static async Task SeedAccountMetadataAsync(IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM WhatsAppAccounts WHERE TenantId = 1 AND Code = N'UAE')
                INSERT INTO WhatsAppAccounts
                    (TenantId, Code, Name, DisplayPhoneNumber, Purpose, CountryCode, Country, IsDefault, IsActive, Status)
                VALUES (1, N'UAE', N'SheikhGo UAE', N'+971557701219', N'Sales', N'+971', N'AE', 1, 1, N'Active');

            IF NOT EXISTS (SELECT 1 FROM WhatsAppAccounts WHERE TenantId = 1 AND Code = N'PK')
                INSERT INTO WhatsAppAccounts
                    (TenantId, Code, Name, DisplayPhoneNumber, Purpose, CountryCode, Country, IsDefault, IsActive, Status)
                VALUES (1, N'PK', N'SheikhGo Pakistan', N'+923177368305', N'Support', N'+92', N'PK', 0, 1, N'Active');

            UPDATE WhatsAppAccounts
            SET CountryCode = COALESCE(CountryCode, N'+971'),
                Country = COALESCE(Country, N'AE'),
                IsDefault = 1,
                Status = COALESCE(NULLIF(LTRIM(RTRIM(Status)), ''), N'Active'),
                Name = COALESCE(NULLIF(LTRIM(RTRIM(Name)), ''), N'SheikhGo UAE'),
                DisplayPhoneNumber = COALESCE(NULLIF(LTRIM(RTRIM(DisplayPhoneNumber)), ''), N'+971557701219'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = 1 AND Code = N'UAE';

            UPDATE WhatsAppAccounts
            SET CountryCode = COALESCE(CountryCode, N'+92'),
                Country = COALESCE(Country, N'PK'),
                IsDefault = CASE WHEN IsDefault = 1 AND Code <> N'UAE' THEN 0 ELSE IsDefault END,
                Status = COALESCE(NULLIF(LTRIM(RTRIM(Status)), ''), N'Active'),
                Name = COALESCE(NULLIF(LTRIM(RTRIM(Name)), ''), N'SheikhGo Pakistan'),
                DisplayPhoneNumber = COALESCE(NULLIF(LTRIM(RTRIM(DisplayPhoneNumber)), ''), N'+923177368305'),
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = 1 AND Code = N'PK';

            -- Ensure exactly one default per tenant when UAE exists.
            UPDATE WhatsAppAccounts SET IsDefault = 0
            WHERE TenantId = 1 AND Code <> N'UAE' AND IsDefault = 1
              AND EXISTS (SELECT 1 FROM WhatsAppAccounts a2 WHERE a2.TenantId = 1 AND a2.Code = N'UAE');
            """, cancellationToken: ct));
    }

    private static async Task AddColumnIfMissingAsync(
        IDbConnection connection, string table, string column, string sqlType, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF COL_LENGTH(N'{table}', N'{column}') IS NULL
                ALTER TABLE [{table}] ADD [{column}] {sqlType};
            """, cancellationToken: ct));
    }

    private static async Task DropColumnIfExistsAsync(
        IDbConnection connection, string table, string column, CancellationToken ct)
    {
        if (!await ColumnExistsAsync(connection, table, column, ct))
            return;

        // Drop default constraints bound to the column (SQL Server blocks DROP COLUMN otherwise).
        await connection.ExecuteAsync(new CommandDefinition("""
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(@Table) + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
            WHERE dc.parent_object_id = OBJECT_ID(@Table) AND c.name = @Column;
            IF LEN(@sql) > 0 EXEC sp_executesql @sql;
            """, new { Table = table, Column = column }, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition($"""
            ALTER TABLE [{table}] DROP COLUMN [{column}];
            """, cancellationToken: ct));
    }

    private static async Task EnsureIndexAsync(
        IDbConnection connection, string indexName, string createSql, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{indexName}')
                {createSql};
            """, cancellationToken: ct));
    }

    private static async Task<bool> ColumnExistsAsync(
        IDbConnection connection, string table, string column, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN COL_LENGTH(@Table, @Column) IS NULL THEN 0 ELSE 1 END
            """, new { Table = table, Column = column }, cancellationToken: ct));
        return n == 1;
    }

    private static async Task<bool> TableExistsAsync(IDbConnection connection, string table, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(@Table, N'U') IS NULL THEN 0 ELSE 1 END
            """, new { Table = table }, cancellationToken: ct));
        return n == 1;
    }
}
