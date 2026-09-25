using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class WhatsAppInboxRepository(IDbConnectionFactory dbFactory) : IWhatsAppInboxRepository
{
    private const string AccountSelect = """
        SELECT Id, TenantId, Code, Name, DisplayPhoneNumber, Purpose, PhoneNumberId, BusinessAccountId, IsActive,
               CAST(ISNULL(IsDefault, 0) AS BIT) AS IsDefault
        FROM WhatsAppAccounts
        """;

    private const string AccountListSelect = """
        SELECT Id, Code, Name AS DisplayName, DisplayPhoneNumber AS E164Phone, Purpose, PhoneNumberId, IsActive,
               CAST(0 AS BIT) AS HasAccessToken,
               Country, CountryCode,
               COALESCE(NULLIF(LTRIM(RTRIM(Status)), ''), CASE WHEN IsActive = 1 THEN N'Active' ELSE N'Inactive' END) AS Status,
               CAST(ISNULL(IsDefault, 0) AS BIT) AS IsDefault,
               LastHealthCheckedAtUtc, LastHealthStatus, LastHealthMessage
        FROM WhatsAppAccounts
        """;

    public async Task<IReadOnlyList<WhatsAppAccountDto>> GetAccountsAsync(
        int tenantId,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<WhatsAppAccountDto>(new CommandDefinition($"""
            {AccountListSelect}
            WHERE TenantId = @TenantId
              AND (@IncludeInactive = 1 OR IsActive = 1)
            ORDER BY CASE Code WHEN N'UAE' THEN 0 WHEN N'PK' THEN 1 ELSE 2 END, Id
            """, new { TenantId = tenantId, IncludeInactive = includeInactive ? 1 : 0 }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<bool> SetAccountActiveAsync(
        int tenantId, int accountId, bool isActive, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var status = isActive ? "Active" : "Inactive";
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAccounts
            SET IsActive = @IsActive,
                Status = @Status,
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = accountId, IsActive = isActive, Status = status },
            cancellationToken: ct));
        return rows > 0;
    }

    public async Task UpdateAccountHealthAsync(
        int tenantId,
        int accountId,
        string status,
        string? message,
        DateTime checkedAtUtc,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAccounts
            SET LastHealthCheckedAtUtc = @CheckedAtUtc,
                LastHealthStatus = @Status,
                LastHealthMessage = @Message,
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new
            {
                TenantId = tenantId,
                Id = accountId,
                CheckedAtUtc = checkedAtUtc,
                Status = Truncate(status, 40),
                Message = Truncate(message, 500)
            }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<WhatsAppAccountRow>> ListActiveAccountRowsAsync(CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<WhatsAppAccountRow>(new CommandDefinition($"""
            {AccountSelect} WHERE IsActive = 1
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<WhatsAppAccountRow?> GetAccountByIdAsync(int tenantId, int accountId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppAccountRow>(new CommandDefinition($"""
            {AccountSelect} WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = accountId }, cancellationToken: ct));
    }

    public async Task<WhatsAppAccountRow?> GetAccountByPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppAccountRow>(new CommandDefinition($"""
            {AccountSelect} WHERE PhoneNumberId = @PhoneNumberId AND IsActive = 1
            """, new { PhoneNumberId = phoneNumberId }, cancellationToken: ct));
    }

    private const string ConversationSelect = """
        SELECT c.Id, c.AccountId, a.Code AS AccountCode,
               c.Id AS ContactId,
               c.CustomerPhoneNumber AS ContactPhone,
               c.CustomerName AS ContactName,
               c.CustomerId,
               c.Status, c.LastMessageAt, c.UnreadCount,
               (
                   SELECT TOP 1 LEFT(COALESCE(m.Text, N''), 500)
                   FROM WhatsAppMessages m
                   WHERE m.ConversationId = c.Id AND m.TenantId = c.TenantId
                   ORDER BY m.CreatedAt DESC, m.Id DESC
               ) AS LastMessagePreview,
               c.AssignedUserId,
               u.FullName AS AssignedUserName,
               c.LeadId,
               lead.Status AS LeadStatus,
               COALESCE(NULLIF(LTRIM(RTRIM(lead.Company)), N''), NULL) AS CustomerCompany,
               CAST(ISNULL(c.IsBotEnabled, 0) AS BIT) AS IsBotEnabled,
               c.CurrentBotState,
               COALESCE(a.Country, CASE
                   WHEN c.CustomerPhoneNumber LIKE N'971%' OR c.CustomerPhoneNumber LIKE N'+971%' THEN N'AE'
                   WHEN c.CustomerPhoneNumber LIKE N'92%' OR c.CustomerPhoneNumber LIKE N'+92%' THEN N'PK'
                   ELSE NULL END) AS Country,
               c.LastIncomingMessageAt,
               c.LastOutgoingMessageAt
        FROM WhatsAppConversations c
        INNER JOIN WhatsAppAccounts a ON a.Id = c.AccountId
        LEFT JOIN Users u ON u.Id = c.AssignedUserId
        LEFT JOIN Customers cust ON cust.Id = c.CustomerId
        LEFT JOIN WebsiteContactRequests lead ON lead.Id = c.LeadId
        """;

    public async Task<(IReadOnlyList<WhatsAppConversationDto> Items, int Total)> GetConversationsAsync(
        int tenantId,
        int? accountId,
        string? search,
        string? filter,
        int? currentUserId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var filterKey = (filter ?? "all").Trim().ToLowerInvariant();
        var searchLike = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%";

        var filterSql = filterKey switch
        {
            "unread" => " AND c.UnreadCount > 0",
            "assigned_to_me" => " AND c.AssignedUserId = @CurrentUserId",
            "unassigned" => " AND c.AssignedUserId IS NULL",
            "bot" => " AND ISNULL(c.IsBotEnabled, 0) = 1",
            "human" => " AND ISNULL(c.IsBotEnabled, 0) = 0",
            "open" => " AND LOWER(c.Status) = N'open'",
            "pending" => " AND LOWER(c.Status) = N'pending'",
            "resolved" => " AND LOWER(c.Status) = N'resolved'",
            _ => ""
        };

        var searchSql = """
              AND (
                @SearchLike IS NULL
                OR c.CustomerPhoneNumber LIKE @SearchLike
                OR c.CustomerName LIKE @SearchLike
                OR cust.FullName LIKE @SearchLike
                OR lead.Company LIKE @SearchLike
                OR lead.FirstName LIKE @SearchLike
                OR lead.LastName LIKE @SearchLike
                OR lead.Phone LIKE @SearchLike
              )
            """;

        var where = $"""
            WHERE c.TenantId = @TenantId
              AND (@AccountId IS NULL OR c.AccountId = @AccountId)
            {filterSql}
            {searchSql}
            """;

        using var connection = dbFactory.CreateConnection();
        var args = new
        {
            TenantId = tenantId,
            AccountId = accountId,
            SearchLike = searchLike,
            CurrentUserId = currentUserId,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1)
            FROM WhatsAppConversations c
            INNER JOIN WhatsAppAccounts a ON a.Id = c.AccountId
            LEFT JOIN Customers cust ON cust.Id = c.CustomerId
            LEFT JOIN WebsiteContactRequests lead ON lead.Id = c.LeadId
            {where}
            """, args, cancellationToken: ct));

        var items = (await connection.QueryAsync<WhatsAppConversationDto>(new CommandDefinition($"""
            {ConversationSelect}
            {where}
            ORDER BY c.LastMessageAt DESC, c.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, args, cancellationToken: ct))).ToList();

        return (items, total);
    }

    public async Task<WhatsAppConversationDto?> GetConversationAsync(int tenantId, int conversationId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppConversationDto>(new CommandDefinition($"""
            {ConversationSelect}
            WHERE c.TenantId = @TenantId AND c.Id = @Id
            """, new { TenantId = tenantId, Id = conversationId }, cancellationToken: ct));
    }

    public async Task<(IReadOnlyList<WhatsAppMessageDto> Items, int Total)> GetMessagesAsync(
        int tenantId, int conversationId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        using var connection = dbFactory.CreateConnection();
        var owns = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM WhatsAppConversations WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId }, cancellationToken: ct));
        if (owns == 0) return ([], 0);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM WhatsAppMessages WHERE ConversationId = @ConversationId AND TenantId = @TenantId
            """, new { ConversationId = conversationId, TenantId = tenantId }, cancellationToken: ct));

        // Page 1 = most recent window; higher pages load older windows. Reverse to ASC for UI.
        var newestFirst = (await connection.QueryAsync<WhatsAppMessageDto>(new CommandDefinition("""
            SELECT Id, ConversationId, Direction,
                   MessageId AS MetaMessageId,
                   MessageType AS Type,
                   Text AS Body,
                   MediaId, Status, CreatedAt AS CreatedAtUtc
            FROM WhatsAppMessages
            WHERE ConversationId = @ConversationId AND TenantId = @TenantId
            ORDER BY CreatedAt DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, new
            {
                ConversationId = conversationId,
                TenantId = tenantId,
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            }, cancellationToken: ct))).ToList();

        newestFirst.Reverse();
        return (newestFirst, total);
    }

    public async Task MarkConversationReadAsync(int tenantId, int conversationId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations SET UnreadCount = 0, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId }, cancellationToken: ct));
    }

    public async Task<bool> SetConversationAssignmentAsync(
        int tenantId, int conversationId, int? assignedUserId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET AssignedUserId = @AssignedUserId, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId, AssignedUserId = assignedUserId },
            cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetConversationStatusAsync(
        int tenantId, int conversationId, string status, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET Status = @Status, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId, Status = status },
            cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetConversationBotAsync(
        int tenantId, int conversationId, bool isBotEnabled, string? currentBotState, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET IsBotEnabled = @IsBotEnabled,
                CurrentBotState = @CurrentBotState,
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new
            {
                TenantId = tenantId,
                Id = conversationId,
                IsBotEnabled = isBotEnabled,
                CurrentBotState = Truncate(currentBotState, 100)
            }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> MessageExistsAsync(string messageId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM WhatsAppMessages WHERE MessageId = @MessageId
            """, new { MessageId = messageId }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<WhatsAppMessageStatusUpdate?> UpdateMessageStatusAsync(
        string messageId, string status, string? errorMessage = null, CancellationToken ct = default)
    {
        var key = status.Trim().ToLowerInvariant();
        var persisted = key switch
        {
            "sent" => "Sent",
            "delivered" => "Delivered",
            "read" => "Read",
            "failed" => "Failed",
            "queued" => "Queued",
            "sending" => "Sending",
            _ => null
        };
        if (persisted is null)
            return null;

        using var connection = dbFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<(int Id, int TenantId, int ConversationId)>(new CommandDefinition("""
            SELECT TOP 1 Id, TenantId, ConversationId
            FROM WhatsAppMessages
            WHERE MessageId = @MessageId
            """, new { MessageId = messageId }, cancellationToken: ct));

        if (row.Id == 0)
            return null;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppMessages
            SET Status = @Status,
                SentAt = CASE WHEN @Status = N'Sent' THEN COALESCE(SentAt, SYSUTCDATETIME()) ELSE SentAt END,
                DeliveredAt = CASE WHEN @Status = N'Delivered' THEN COALESCE(DeliveredAt, SYSUTCDATETIME()) ELSE DeliveredAt END,
                ReadAt = CASE WHEN @Status = N'Read' THEN COALESCE(ReadAt, SYSUTCDATETIME()) ELSE ReadAt END,
                ErrorMessage = CASE
                    WHEN @Status = N'Failed' AND @ErrorMessage IS NOT NULL THEN @ErrorMessage
                    ELSE ErrorMessage
                END
            WHERE MessageId = @MessageId
            """, new { MessageId = messageId, Status = persisted, ErrorMessage = Truncate(errorMessage, 1000) },
            cancellationToken: ct));

        return new WhatsAppMessageStatusUpdate(
            row.TenantId,
            row.ConversationId,
            row.Id,
            messageId,
            persisted);
    }

    public async Task<int> UpsertInboundMessageAsync(WhatsAppInboundPersistRequest request, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        using var tx = connection.BeginTransaction();
        try
        {
            var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                SELECT Id FROM WhatsAppMessages WHERE MessageId = @MessageId
                """, new { request.MessageId }, transaction: tx, cancellationToken: ct));
            if (existing is not null)
            {
                tx.Commit();
                return existing.Value;
            }

            var phone = string.IsNullOrWhiteSpace(request.PhoneE164) ? request.WaId : request.PhoneE164;
            var conversationId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                SELECT Id FROM WhatsAppConversations
                WHERE TenantId = @TenantId AND AccountId = @AccountId AND CustomerPhoneNumber = @Phone
                """, new { request.TenantId, request.AccountId, Phone = phone }, transaction: tx, cancellationToken: ct));

            var customerId = await FindCustomerIdByPhoneInTxAsync(connection, tx, request.TenantId, phone, ct);
            var leadId = await FindWebsiteContactRequestIdByPhoneInTxAsync(connection, tx, request.TenantId, phone, ct);

            if (conversationId is null)
            {
                conversationId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO WhatsAppConversations
                        (TenantId, AccountId, CustomerPhoneNumber, CustomerName, CustomerId, LeadId, Status,
                         LastMessageAt, LastIncomingMessageAt, UnreadCount, IsBotEnabled, CurrentBotState)
                    OUTPUT INSERTED.Id
                    VALUES (@TenantId, @AccountId, @Phone, @Name, @CustomerId, @LeadId, N'Open',
                            SYSUTCDATETIME(), SYSUTCDATETIME(), 1, 1, N'Idle')
                    """, new
                    {
                        request.TenantId,
                        request.AccountId,
                        Phone = phone,
                        Name = request.ProfileName,
                        CustomerId = customerId,
                        LeadId = leadId
                    }, transaction: tx, cancellationToken: ct));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE WhatsAppConversations
                    SET LastMessageAt = SYSUTCDATETIME(),
                        LastIncomingMessageAt = SYSUTCDATETIME(),
                        UnreadCount = UnreadCount + 1,
                        Status = N'Open',
                        CustomerName = COALESCE(@Name, CustomerName),
                        CustomerId = COALESCE(CustomerId, @CustomerId),
                        LeadId = COALESCE(LeadId, @LeadId),
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = @Id
                    """, new
                    {
                        Id = conversationId.Value,
                        Name = request.ProfileName,
                        CustomerId = customerId,
                        LeadId = leadId
                    }, transaction: tx, cancellationToken: ct));
            }

            var messageId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO WhatsAppMessages
                    (TenantId, ConversationId, AccountId, Direction, MessageId, MessageType, Text, MediaId, Status, RawPayload)
                OUTPUT INSERTED.Id
                VALUES (@TenantId, @ConversationId, @AccountId, N'Inbound', @MessageId, @Type, @Body, @MediaId, N'received', @RawPayload)
                """, new
                {
                    request.TenantId,
                    ConversationId = conversationId.Value,
                    request.AccountId,
                    request.MessageId,
                    request.Type,
                    request.Body,
                    request.MediaId,
                    request.RawPayload
                }, transaction: tx, cancellationToken: ct));

            tx.Commit();
            return messageId;
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            tx.Rollback();
            using var connection2 = dbFactory.CreateConnection();
            var id = await connection2.ExecuteScalarAsync<int?>(new CommandDefinition("""
                SELECT Id FROM WhatsAppMessages WHERE MessageId = @MessageId
                """, new { request.MessageId }, cancellationToken: ct));
            return id ?? 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<int> InsertOutboundMessageAsync(
        int tenantId, int conversationId, string? messageId, string type, string? body, string status,
        CancellationToken ct = default, string? templateName = null)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var accountId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT AccountId FROM WhatsAppConversations WHERE Id = @Id AND TenantId = @TenantId
                """, new { Id = conversationId, TenantId = tenantId }, transaction: tx, cancellationToken: ct));

            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO WhatsAppMessages
                    (TenantId, ConversationId, AccountId, Direction, MessageId, MessageType, Text, TemplateName, Status, SentAt)
                OUTPUT INSERTED.Id
                VALUES (@TenantId, @ConversationId, @AccountId, N'Outbound', @MessageId, @Type, @Body, @TemplateName, @Status,
                        CASE WHEN @Status IN (N'Sent', N'Delivered', N'Read', N'sent') THEN SYSUTCDATETIME() ELSE NULL END)
                """, new
                {
                    TenantId = tenantId,
                    ConversationId = conversationId,
                    AccountId = accountId,
                    MessageId = messageId,
                    Type = type,
                    Body = body,
                    TemplateName = Truncate(templateName, 200),
                    Status = NormalizeOutboundStatus(status)
                }, transaction: tx, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppConversations
                SET LastMessageAt = SYSUTCDATETIME(),
                    LastOutgoingMessageAt = SYSUTCDATETIME(),
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new { Id = conversationId, TenantId = tenantId }, transaction: tx, cancellationToken: ct));

            tx.Commit();
            return id;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task SetOutboundMetaIdAsync(int messageId, string metaMessageId, string status, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var persisted = NormalizeOutboundStatus(status);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppMessages
            SET MessageId = @MessageId,
                Status = @Status,
                SentAt = CASE WHEN @Status = N'Sent' THEN COALESCE(SentAt, SYSUTCDATETIME()) ELSE SentAt END
            WHERE Id = @Id
            """, new { Id = messageId, MessageId = metaMessageId, Status = persisted }, cancellationToken: ct));
    }

    public async Task SetOutboundStatusAsync(int messageId, string status, string? errorMessage = null, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var persisted = NormalizeOutboundStatus(status);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppMessages
            SET Status = @Status,
                SentAt = CASE WHEN @Status = N'Sent' THEN COALESCE(SentAt, SYSUTCDATETIME()) ELSE SentAt END,
                ErrorMessage = CASE
                    WHEN @Status = N'Failed' AND @ErrorMessage IS NOT NULL THEN @ErrorMessage
                    ELSE ErrorMessage
                END
            WHERE Id = @Id
            """, new { Id = messageId, Status = persisted, ErrorMessage = Truncate(errorMessage, 1000) },
            cancellationToken: ct));
    }

    public async Task<DateTime?> GetLastIncomingMessageAtAsync(
        int tenantId, int accountId, string customerPhoneNumber, CancellationToken ct = default)
    {
        var phone = string.IsNullOrWhiteSpace(customerPhoneNumber)
            ? ""
            : (customerPhoneNumber.StartsWith('+')
                ? customerPhoneNumber
                : WhatsAppPhoneNormalize(customerPhoneNumber));
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition("""
            SELECT TOP 1 LastIncomingMessageAt
            FROM WhatsAppConversations
            WHERE TenantId = @TenantId AND AccountId = @AccountId AND CustomerPhoneNumber = @Phone
            ORDER BY COALESCE(LastMessageAt, CreatedAt) DESC
            """, new { TenantId = tenantId, AccountId = accountId, Phone = phone }, cancellationToken: ct));
    }

    public async Task<int> EnsureConversationAsync(
        int tenantId,
        int accountId,
        string customerPhoneNumber,
        string? customerName = null,
        CancellationToken ct = default)
    {
        var phone = string.IsNullOrWhiteSpace(customerPhoneNumber)
            ? ""
            : (customerPhoneNumber.StartsWith('+') ? customerPhoneNumber : WhatsAppPhoneNormalize(customerPhoneNumber));
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Customer phone is required.", nameof(customerPhoneNumber));

        using var connection = dbFactory.CreateConnection();
        var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT Id FROM WhatsAppConversations
            WHERE TenantId = @TenantId AND AccountId = @AccountId AND CustomerPhoneNumber = @Phone
            """, new { TenantId = tenantId, AccountId = accountId, Phone = phone }, cancellationToken: ct));
        if (existing is not null)
            return existing.Value;

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WhatsAppConversations
                (TenantId, AccountId, CustomerPhoneNumber, CustomerName, Status, UnreadCount)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @AccountId, @Phone, @Name, N'Open', 0)
            """, new
            {
                TenantId = tenantId,
                AccountId = accountId,
                Phone = phone,
                Name = customerName
            }, cancellationToken: ct));
    }

    private static string WhatsAppPhoneNormalize(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digits) ? raw : "+" + digits;
    }

    private static string NormalizeOutboundStatus(string status)
    {
        var key = status.Trim().ToLowerInvariant();
        return key switch
        {
            "queued" => "Queued",
            "sending" => "Sending",
            "sent" => "Sent",
            "delivered" => "Delivered",
            "read" => "Read",
            "failed" => "Failed",
            _ => status.Trim()
        };
    }

    /// <summary>Resolves conversation identity for CRM linking (id is conversation id).</summary>
    public async Task<WhatsAppContactDto?> GetContactAsync(int tenantId, int conversationId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppContactDto>(new CommandDefinition("""
            SELECT c.Id, c.AccountId,
                   c.CustomerPhoneNumber AS WaId,
                   c.CustomerPhoneNumber AS PhoneE164,
                   c.CustomerName AS ProfileName,
                   c.CustomerId,
                   cust.FullName AS SuggestedCustomerName
            FROM WhatsAppConversations c
            LEFT JOIN Customers cust ON cust.Id = c.CustomerId
            WHERE c.TenantId = @TenantId AND c.Id = @Id
            """, new { TenantId = tenantId, Id = conversationId }, cancellationToken: ct));
    }

    public async Task LinkCustomerAsync(int tenantId, int conversationId, int? customerId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations SET CustomerId = @CustomerId, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId, CustomerId = customerId }, cancellationToken: ct));
    }

    public async Task<int?> FindCustomerIdByPhoneAsync(int tenantId, string phoneE164, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await FindCustomerIdByPhoneInTxAsync(connection, null, tenantId, phoneE164, ct);
    }

    public async Task<int?> FindWebsiteContactRequestIdByPhoneAsync(int tenantId, string phoneE164, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await FindWebsiteContactRequestIdByPhoneInTxAsync(connection, null, tenantId, phoneE164, ct);
    }

    public async Task InsertWebhookDeadLetterAsync(string reason, string? payloadPreview, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO WhatsAppWebhookDeadLetters (Reason, PayloadPreview)
            VALUES (@Reason, @PayloadPreview)
            """, new { Reason = Truncate(reason, 400), PayloadPreview = Truncate(payloadPreview, 2000) }, cancellationToken: ct));
    }

    public async Task DeleteOldMessagesAsync(int retentionDays, CancellationToken ct = default)
    {
        retentionDays = Math.Clamp(retentionDays, 30, 3650);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM WhatsAppMessages
            WHERE CreatedAt < DATEADD(DAY, -@Days, SYSUTCDATETIME())
            """, new { Days = retentionDays }, cancellationToken: ct));
    }

    private static async Task<int?> FindCustomerIdByPhoneInTxAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? tx,
        int tenantId,
        string phoneE164,
        CancellationToken ct)
    {
        var digits = new string(phoneE164.Where(char.IsDigit).ToArray());
        if (digits.Length < 8) return null;
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Customers
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND REPLACE(REPLACE(REPLACE(ISNULL(Phone,''),'+',''),' ',''),'-','') LIKE '%' + @Digits
            ORDER BY Id DESC
            """, new { TenantId = tenantId, Digits = digits }, transaction: tx, cancellationToken: ct));
    }

    private static async Task<int?> FindWebsiteContactRequestIdByPhoneInTxAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? tx,
        int tenantId,
        string phoneE164,
        CancellationToken ct)
    {
        var digits = new string(phoneE164.Where(char.IsDigit).ToArray());
        if (digits.Length < 8) return null;

        // Table may be absent in some environments — fail soft.
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'WebsiteContactRequests', N'U') IS NULL THEN 0 ELSE 1 END
            """, transaction: tx, cancellationToken: ct));
        if (exists == 0) return null;

        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM WebsiteContactRequests
            WHERE TenantId = @TenantId
              AND REPLACE(REPLACE(REPLACE(ISNULL(Phone,''),'+',''),' ',''),'-','') LIKE '%' + @Digits
            ORDER BY
                CASE WHEN Status IN (N'New', N'Contacted', N'InProgress', N'Qualified', N'Open', N'Pending') THEN 0 ELSE 1 END,
                Id DESC
            """, new { TenantId = tenantId, Digits = digits }, transaction: tx, cancellationToken: ct));
    }

    public async Task LinkLeadAsync(int tenantId, int conversationId, int? leadId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET LeadId = @LeadId, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId, LeadId = leadId }, cancellationToken: ct));
    }

    public async Task LinkCustomerAndLeadAsync(
        int tenantId,
        int conversationId,
        int? customerId,
        int? leadId,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET CustomerId = COALESCE(CustomerId, @CustomerId),
                LeadId = COALESCE(LeadId, @LeadId),
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new
            {
                TenantId = tenantId,
                Id = conversationId,
                CustomerId = customerId,
                LeadId = leadId
            }, cancellationToken: ct));
    }

    public async Task<int> CreateWhatsAppLeadAsync(WhatsAppLeadCreateRequest request, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var name = string.IsNullOrWhiteSpace(request.CustomerName) ? "WhatsApp" : request.CustomerName.Trim();
        var first = name.Length > 80 ? name[..80] : name;
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WebsiteContactRequests
                (TenantId, FirstName, LastName, Company, Email, Phone, Country, FleetSize, InterestedIn, Message,
                 Status, Source, WhatsAppConversationId, WhatsAppAccountId)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @FirstName, N'', N'WhatsApp', N'', @Phone, @Country, NULL, NULL,
                 N'Inbound WhatsApp conversation',
                 N'New', N'WhatsApp', @ConversationId, @AccountId)
            """, new
            {
                request.TenantId,
                FirstName = first,
                Phone = request.PhoneE164,
                Country = Truncate(request.Country, 80),
                ConversationId = request.ConversationId,
                AccountId = request.AccountId
            }, cancellationToken: ct));
    }

    public async Task UpdateWhatsAppLeadQualificationAsync(
        WhatsAppLeadQualificationUpdate request, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteContactRequests
            SET FleetType = COALESCE(@FleetType, FleetType),
                FleetSize = COALESCE(@FleetSize, FleetSize),
                InterestedIn = COALESCE(@FleetType, InterestedIn),
                MainChallenge = COALESCE(@MainChallenge, MainChallenge),
                Status = COALESCE(@Status, Status),
                Message = COALESCE(@Message, Message),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @LeadId AND TenantId = @TenantId
            """, new
            {
                request.TenantId,
                request.LeadId,
                FleetType = Truncate(request.FleetType, 120),
                FleetSize = Truncate(request.FleetSize, 80),
                MainChallenge = Truncate(request.MainChallenge, 120),
                request.Status,
                Message = request.Message
            }, cancellationToken: ct));
    }

    public async Task AttachWhatsAppLeadLinksAsync(
        int tenantId,
        int leadId,
        int conversationId,
        int accountId,
        string? sourceIfEmpty = "WhatsApp",
        string? country = null,
        string? customerName = null,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var first = string.IsNullOrWhiteSpace(customerName) ? null : Truncate(customerName.Trim(), 80);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteContactRequests
            SET WhatsAppConversationId = COALESCE(WhatsAppConversationId, @ConversationId),
                WhatsAppAccountId = COALESCE(WhatsAppAccountId, @AccountId),
                Source = CASE
                    WHEN Source IS NULL OR LTRIM(RTRIM(Source)) = N'' THEN @Source
                    ELSE Source
                END,
                Country = COALESCE(NULLIF(LTRIM(RTRIM(Country)), N''), @Country),
                FirstName = CASE
                    WHEN @FirstName IS NOT NULL AND (FirstName IS NULL OR FirstName IN (N'', N'WhatsApp'))
                        THEN @FirstName
                    ELSE FirstName
                END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @LeadId AND TenantId = @TenantId
            """, new
            {
                TenantId = tenantId,
                LeadId = leadId,
                ConversationId = conversationId,
                AccountId = accountId,
                Source = sourceIfEmpty ?? "WhatsApp",
                Country = Truncate(country, 80),
                FirstName = first
            }, cancellationToken: ct));
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("IX_WhatsAppMessages_MessageId", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("unique index", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("2627", StringComparison.Ordinal)
                || e.Message.Contains("2601", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
