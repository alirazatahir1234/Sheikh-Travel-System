using System.Data;
using Dapper;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Per-tenant, per-year document numbers (<c>ENQ-2026-00001</c>). Must be called inside the transaction that
/// inserts the document: the row lock serialises concurrent callers and a rollback releases the number,
/// so committed numbers have no gaps (unlike <c>MAX(Id)+1</c>).
/// </summary>
internal static class TravelNumberAllocator
{
    public enum Kind { Enquiry, VisaCase, Group, Booking, Invoice, Receipt, CreditNote, Refund }

    public static async Task<string> NextAsync(
        IDbConnection connection, IDbTransaction transaction, int tenantId, Kind kind, DateTime utcNow, CancellationToken ct)
    {
        var (prefixColumn, defaultPrefix) = kind switch
        {
            Kind.Enquiry => ("EnquiryPrefix", "ENQ"),
            Kind.VisaCase => ("VisaCasePrefix", "VC"),
            Kind.Group => ("GroupPrefix", "GRP"),
            Kind.Booking => ("BookingPrefix", "BKG"),
            Kind.Invoice => ("InvoicePrefix", "TINV"),
            Kind.Receipt => ("ReceiptPrefix", "TRCP"),
            Kind.CreditNote => ("CreditNotePrefix", "TCN"),
            _ => ("RefundPrefix", "TRF")
        };
        var prefix = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT {prefixColumn} FROM TravelSettings WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, transaction, cancellationToken: ct)) ?? defaultPrefix;

        var year = utcNow.Year;
        var key = $"{defaultPrefix}-{year}";
        var value = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            DECLARE @Value INT;
            -- Column references in SET read the pre-update row, so @Value receives the current NextValue.
            -- HOLDLOCK takes a key-range lock even when no row exists, serialising the first allocation too.
            UPDATE TravelNumberSequences WITH (UPDLOCK, HOLDLOCK)
            SET @Value = NextValue, NextValue = NextValue + 1
            WHERE TenantId = @TenantId AND SequenceKey = @Key;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO TravelNumberSequences (TenantId, SequenceKey, NextValue) VALUES (@TenantId, @Key, 2);
                SET @Value = 1;
            END

            SELECT @Value;
            """, new { TenantId = tenantId, Key = key }, transaction, cancellationToken: ct));

        return Format(prefix, year, value);
    }

    public static string Format(string prefix, int year, int value) => $"{prefix}-{year}-{value:D5}";
}
