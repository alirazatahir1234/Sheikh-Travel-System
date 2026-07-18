namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IDatabaseMigrationRunner
{
    Task EnsureHistoryTableAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchemaMigrationStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<SchemaMigrationApplyResultDto> ApplyPendingAsync(
        string appliedBy,
        CancellationToken cancellationToken = default);
}

public sealed class SchemaMigrationStatusDto
{
    public required string Name { get; init; }
    public required int Order { get; init; }
    public required bool IsApplied { get; init; }
    public DateTime? AppliedAtUtc { get; init; }
    public string? AppliedBy { get; init; }
}

public sealed class SchemaMigrationApplyResultDto
{
    public int AppliedCount { get; init; }
    public int SkippedCount { get; init; }
    public IReadOnlyList<string> AppliedNames { get; init; } = Array.Empty<string>();
    public string? FailedMigration { get; init; }
    public string? ErrorMessage { get; init; }
}
