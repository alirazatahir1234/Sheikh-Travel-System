using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Tests.Security;

/// <summary>
/// The seeder gives the demo accounts passwords that are public (they are in this repository) and resets most of
/// them on every start. That may happen in Development only. Anywhere else the seeder must not write a password or
/// re-activate an account, and it must say so when an account still uses a public password.
/// </summary>
public class DatabaseSeederDemoAccountTests
{
    private const string CheckQuery = "SELECT Email, PasswordHash FROM Users";

    // Any statement that sets a password, creates a user, re-activates one, or rewires its roles or driver link.
    private static readonly Regex WritesAnAccount = new(
        @"\bPasswordHash\s*=\s*@|INSERT\s+INTO\s+Users\b|\bIsActive\s*=\s*1\s*,\s*IsDeleted\s*=\s*0|INSERT\s+INTO\s+UserRoles\b|DELETE\s+ur\b|UPDATE\s+d\s+SET\s+d\.UserId",
        RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("UAT")]
    public async Task Outside_Development_no_password_is_written_and_no_account_is_reactivated(string environmentName)
    {
        var sut = Build(environmentName);

        await sut.Seeder.SeedAsync();

        sut.Hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        sut.Connection.Sql.Where(sql => WritesAnAccount.IsMatch(sql)).Should().BeEmpty();
        sut.Connection.Sql.Should().Contain(sql => sql.Contains("ALTER TABLE"), "schema evolution still runs everywhere");
        sut.Log.Messages(LogLevel.Information).Should().Contain(
            $"Demo accounts are not seeded or reset in the {environmentName} environment.");
        sut.Log.Entries.Should().NotContain(e => e.Message.StartsWith("Updated password for"));
        sut.Log.Messages(LogLevel.Information).Should().Contain("Database seeding complete.");
    }

    [Fact]
    public async Task Development_still_seeds_and_resets_the_demo_accounts()
    {
        var sut = Build(Environments.Development);

        await sut.Seeder.SeedAsync();

        sut.Hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.AtLeastOnce);
        sut.Connection.Sql.Should().Contain(sql => sql.Contains("UPDATE Users SET PasswordHash = @PasswordHash"));
        sut.Log.Messages(LogLevel.Information).Should().Contain("Updated password for admin@sheikhtravel.com");
        // The public-password check is for the other environments only.
        sut.Connection.Sql.Should().NotContain(sql => sql.Contains(CheckQuery));
        sut.Hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Outside_Development_names_the_active_accounts_that_still_use_a_public_password()
    {
        var sut = Build(Environments.Production);
        sut.Connection.Accounts =
        [
            ("admin@sheikhtravel.com", "hash-still-public"),
            ("driver@sheikhtravel.com", "hash-changed"),
            ("owner@chandtravel.com", "hash-still-public"),
        ];
        sut.Hasher.Setup(h => h.Verify(It.IsAny<string>(), "hash-still-public")).Returns(true);

        await sut.Seeder.SeedAsync();

        var critical = sut.Log.Messages(LogLevel.Critical).Should().ContainSingle().Subject;
        critical.Should().StartWith("2 active account(s)");
        critical.Should().Contain("admin@sheikhtravel.com").And.Contain("owner@chandtravel.com");
        critical.Should().NotContain("driver@sheikhtravel.com");

        // It reports accounts, never a password, and it changes nothing.
        var triedPasswords = sut.Hasher.Invocations
            .Where(i => i.Method.Name == nameof(IPasswordHasher.Verify))
            .Select(i => (string)i.Arguments[0])
            .Distinct()
            .ToList();
        triedPasswords.Should().NotBeEmpty();
        sut.Log.Entries.Should().NotContain(e => triedPasswords.Any(p => e.Message.Contains(p)));
        sut.Connection.Sql.Where(sql => WritesAnAccount.IsMatch(sql)).Should().BeEmpty();
    }

    [Fact]
    public async Task Outside_Development_stays_quiet_when_no_account_uses_a_public_password()
    {
        var sut = Build(Environments.Production);
        sut.Connection.Accounts = [("admin@sheikhtravel.com", "hash-changed")];

        await sut.Seeder.SeedAsync();

        sut.Hasher.Verify(h => h.Verify(It.IsAny<string>(), "hash-changed"), Times.AtLeastOnce);
        sut.Log.Messages(LogLevel.Critical).Should().BeEmpty();
    }

    [Fact]
    public async Task An_unreadable_hash_is_skipped_and_does_not_hide_other_accounts()
    {
        var sut = Build(Environments.Production);
        sut.Connection.Accounts =
        [
            ("admin@sheikhtravel.com", "not-a-hash"),
            ("driver@sheikhtravel.com", "hash-still-public"),
            ("dispatcher@sheikhtravel.com", null),
        ];
        sut.Hasher.Setup(h => h.Verify(It.IsAny<string>(), "not-a-hash")).Throws(new FormatException("Invalid salt version"));
        sut.Hasher.Setup(h => h.Verify(It.IsAny<string>(), "hash-still-public")).Returns(true);

        await sut.Seeder.SeedAsync();

        var critical = sut.Log.Messages(LogLevel.Critical).Should().ContainSingle().Subject;
        critical.Should().StartWith("1 active account(s)").And.Contain("driver@sheikhtravel.com");
        critical.Should().NotContain("admin@sheikhtravel.com").And.NotContain("dispatcher@sheikhtravel.com");
    }

    [Fact]
    public async Task A_failing_check_does_not_stop_seeding()
    {
        var sut = Build(Environments.Production);
        sut.Connection.FailOn = CheckQuery;

        await sut.Seeder.SeedAsync();

        sut.Log.Messages(LogLevel.Warning).Should().Contain("Could not check the demo accounts for public passwords.");
        sut.Log.Messages(LogLevel.Information).Should().Contain("Database seeding complete.");
    }

    [Fact]
    public void The_check_asks_only_about_the_demo_accounts()
    {
        DatabaseSeeder.DemoAccountEmails.Should().HaveCount(8).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_demo_address_in_the_seeder_is_in_the_demo_account_list()
    {
        // A demo account added to the seeder without adding it to the list would escape the check.
        var inSource = Regex.Matches(SeederSource(), @"[a-z]+@(?:sheikhtravel|chandtravel)\.com")
            .Select(m => m.Value)
            .Distinct();

        inSource.Should().BeEquivalentTo(DatabaseSeeder.DemoAccountEmails);
    }

    [Fact]
    public void The_seeder_keeps_its_passwords_in_the_two_demo_constants_only()
    {
        var source = SeederSource();

        var literals = Regex.Matches(source, @"\b(?:const\s+string|string|var)\s+(\w*[Pp]assword\w*)\s*=\s*""")
            .Select(m => m.Groups[1].Value);

        literals.Should().BeEquivalentTo(["DefaultDemoPassword", "OwnerDemoPassword"]);
        Regex.IsMatch(source, @"\.Hash\(\s*""").Should().BeFalse("no password literal is hashed inline");
    }

    // ── Test plumbing ───────────────────────────────────────────────────────

    private sealed record Sut(
        DatabaseSeeder Seeder, RecordingConnection Connection, Mock<IPasswordHasher> Hasher, ListLogger Log);

    private static Sut Build(string environmentName)
    {
        var connection = new RecordingConnection();
        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateConnection()).Returns(connection);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var log = new ListLogger();
        return new Sut(new DatabaseSeeder(factory.Object, hasher.Object, environment.Object, log), connection, hasher, log);
    }

    private static string SeederSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "Backend", "SheikhTravelSystem.Infrastructure", "Persistence", "DatabaseSeeder.cs");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate DatabaseSeeder.cs.");
    }

    private sealed class ListLogger : ILogger<DatabaseSeeder>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get { lock (_entries) return _entries.ToList(); }
        }

        public IEnumerable<string> Messages(LogLevel level) => Entries.Where(e => e.Level == level).Select(e => e.Message);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_entries) _entries.Add((logLevel, formatter(state, exception)));
        }
    }

    /// <summary>
    /// Stands in for SQL Server. It records every statement, answers each scalar with 1 (so "does it exist" and
    /// "has the table rows" are both yes) and returns no rows, except for the demo-account check.
    /// </summary>
    private sealed class RecordingConnection : DbConnection
    {
        public List<string> Sql { get; } = [];

        public (string Email, string? PasswordHash)[] Accounts { get; set; } = [];

        public string? FailOn { get; set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = "";

        public override string Database => "fake";

        public override string DataSource => "fake";

        public override string ServerVersion => "0";

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }

        public override void Close() { }

        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new RecordingCommand(this);

        public void Record(string sql)
        {
            Sql.Add(sql);
            if (FailOn is not null && sql.Contains(FailOn))
                throw new InvalidOperationException("Simulated database failure.");
        }

        public DataTable RowsFor(string sql)
        {
            var table = new DataTable();
            if (!sql.Contains(CheckQuery))
                return table;

            table.Columns.Add("Email", typeof(string));
            table.Columns.Add("PasswordHash", typeof(string));
            foreach (var (email, hash) in Accounts)
                table.Rows.Add(email, (object?)hash ?? DBNull.Value);
            return table;
        }
    }

    private sealed class RecordingCommand(RecordingConnection connection) : DbCommand
    {
        private readonly FakeParameters _parameters = new();

        [AllowNull]
        public override string CommandText { get; set; } = "";

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = connection;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }

        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new FakeParameter();

        public override int ExecuteNonQuery()
        {
            connection.Record(CommandText);
            return 0;
        }

        public override object? ExecuteScalar()
        {
            connection.Record(CommandText);
            return 1;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            connection.Record(CommandText);
            return connection.RowsFor(CommandText).CreateDataReader();
        }
    }

    private sealed class FakeParameter : DbParameter
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

    private sealed class FakeParameters : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => _items;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                _items.RemoveAt(index);
        }

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}
