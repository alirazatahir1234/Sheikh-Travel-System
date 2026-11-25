using System.Data;
using Dapper;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Profile fields of a travel customer (Customers.FullName doubles as company name for corporates).</summary>
internal sealed record TravelProfileValues(
    string Kind,
    string FullName,
    string? ContactPerson,
    string? TaxNumber,
    string? AlternatePhone,
    string? City,
    int? AgentId,
    int? BranchId,
    int? LeadSourceId,
    string? Notes);

/// <summary>
/// Customer + travel profile writes shared by customer creation and enquiry conversion. Callers own the
/// transaction; every statement is scoped by <c>TenantId</c>.
/// </summary>
internal static class TravelCustomerSql
{
    public static Task<int> InsertCustomerAsync(
        IDbConnection connection, IDbTransaction tx, int tenantId,
        string fullName, string phone, string? email, string? address, string? cnic, CancellationToken ct)
        => connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Customers (TenantId, FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt, IsDeleted)
            VALUES (@TenantId, @FullName, @Phone, @Email, @Address, @Cnic, 1, SYSUTCDATETIME(), 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, FullName = fullName, Phone = phone, Email = email, Address = address, Cnic = cnic },
            tx, cancellationToken: ct));

    /// <summary>Re-activates an archived profile (unique on TenantId+CustomerId) or inserts a new one.</summary>
    public static Task UpsertProfileAsync(
        IDbConnection connection, IDbTransaction tx, int tenantId, int customerId, TravelProfileValues v, int? userId, CancellationToken ct)
    {
        var p = ProfileParams(tenantId, customerId, v);
        p.Add("UserId", userId);
        return connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelCustomerProfiles SET
                CustomerKind = @Kind, CompanyName = @CompanyName, ContactPerson = @ContactPerson, TaxNumber = @TaxNumber,
                AlternatePhone = @AlternatePhone, City = @City, AssignedAgentUserId = @AgentId, BranchId = @BranchId,
                LeadSourceId = @LeadSourceId, Notes = @Notes, IsDeleted = 0, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND CustomerId = @CustomerId AND IsDeleted = 1;

            IF @@ROWCOUNT = 0
                INSERT INTO TravelCustomerProfiles (TenantId, CustomerId, CustomerKind, CompanyName, ContactPerson, TaxNumber,
                    AlternatePhone, City, AssignedAgentUserId, BranchId, LeadSourceId, Notes, CreatedBy)
                VALUES (@TenantId, @CustomerId, @Kind, @CompanyName, @ContactPerson, @TaxNumber,
                    @AlternatePhone, @City, @AgentId, @BranchId, @LeadSourceId, @Notes, @UserId);
            """, p, tx, cancellationToken: ct));
    }

    public static DynamicParameters ProfileParams(int tenantId, int customerId, TravelProfileValues v)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("CustomerId", customerId);
        p.Add("Kind", v.Kind);
        p.Add("CompanyName", v.Kind == "Corporate" ? v.FullName : null);
        p.Add("ContactPerson", v.ContactPerson);
        p.Add("TaxNumber", v.TaxNumber);
        p.Add("AlternatePhone", v.AlternatePhone);
        p.Add("City", v.City);
        p.Add("AgentId", v.AgentId);
        p.Add("BranchId", v.BranchId);
        p.Add("LeadSourceId", v.LeadSourceId);
        p.Add("Notes", v.Notes);
        return p;
    }
}
