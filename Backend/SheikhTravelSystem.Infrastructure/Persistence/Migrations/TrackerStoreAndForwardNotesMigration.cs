using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Documents store-and-forward reality on GpsDevices.InstallationNotes.
/// Flash buffering is device firmware; Traccar attrs/XML only help ACK/reorder.
/// </summary>
public static class TrackerStoreAndForwardNotesMigration
{
    private const string Vg03Note =
        "STORE-AND-FORWARD: JM-VG03 firmware FIFO flash (32+32Mb); auto-upload on reconnect. " +
        "No BFFREQ SMS in manual. Traccar does not write device flash. See TRACKER_STORE_AND_FORWARD.md.";

    private const string St901Note =
        "STORE-AND-FORWARD: ST-901/H02 firmware buffer (~vendor ~2000) FIFO; needs h02:5013 + h02.ack. " +
        "SMS may be password-locked. Traccar does not write device flash. See TRACKER_STORE_AND_FORWARD.md.";

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;",
            cancellationToken: cancellationToken));

        if (!await ColumnExistsAsync(connection, "GpsDevices", "InstallationNotes", cancellationToken))
        {
            logger.LogInformation("TrackerStoreAndForwardNotesMigration: InstallationNotes column missing — skip.");
            return;
        }

        var vg03 = await AppendNoteAsync(
            connection,
            Vg03Note,
            "%STORE-AND-FORWARD%",
            """
            UniqueId = N'862292058090598'
            OR UniqueId LIKE N'%862292058090598%'
            OR Name LIKE N'%VG03%'
            OR Model LIKE N'%VG03%'
            OR TrackerModelKey = N'jimi_vg03'
            """,
            cancellationToken);

        var st901 = await AppendNoteAsync(
            connection,
            St901Note,
            "%STORE-AND-FORWARD%",
            """
            UniqueId = N'865023071369959'
            OR UniqueId LIKE N'%865023071369959%'
            OR TrackerModelKey = N'sinotrack_st901'
            OR Model LIKE N'%ST-901%'
            OR Model LIKE N'%ST901%'
            OR Model LIKE N'%SOGOTRACK%'
            """,
            cancellationToken);

        logger.LogInformation(
            "TrackerStoreAndForwardNotesMigration: VG03 rows={Vg03}, ST-901 rows={St901}.",
            vg03, st901);
    }

    private static async Task<int> AppendNoteAsync(
        IDbConnection connection,
        string note,
        string alreadyMarkerLike,
        string whereClause,
        CancellationToken ct) =>
        await connection.ExecuteAsync(new CommandDefinition($"""
            UPDATE GpsDevices
            SET InstallationNotes = CASE
                    WHEN InstallationNotes IS NULL OR LTRIM(RTRIM(InstallationNotes)) = N'' THEN @Note
                    WHEN InstallationNotes LIKE @Already THEN InstallationNotes
                    ELSE LEFT(InstallationNotes + N' | ' + @Note, 500)
                END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE IsDeleted = 0
              AND ({whereClause});
            """,
            new { Note = note, Already = alreadyMarkerLike },
            cancellationToken: ct));

    private static async Task<bool> ColumnExistsAsync(
        IDbConnection connection, string table, string column, CancellationToken ct) =>
        await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN COL_LENGTH(@Table, @Column) IS NOT NULL THEN 1 ELSE 0 END
            """, new { Table = table, Column = column }, cancellationToken: ct));
}
