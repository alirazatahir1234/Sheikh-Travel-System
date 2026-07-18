using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public interface IDatabaseMigration
{
    string Name { get; }

    Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default);
}

public sealed class DelegateMigration : IDatabaseMigration
{
    private readonly Func<IDbConnectionFactory, ILogger, CancellationToken, Task> _apply;

    public DelegateMigration(string name, Func<IDbConnectionFactory, ILogger, CancellationToken, Task> apply)
    {
        Name = name;
        _apply = apply;
    }

    public string Name { get; }

    public Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
        => _apply(dbFactory, logger, cancellationToken);
}
