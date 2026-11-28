using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Infrastructure.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// <see cref="NominatimReverseGeocodingService.GetCachedAddressAsync"/> is what a History replay names its
/// places with, so it must answer from GpsAddressCache alone and never wait for a provider.
/// </summary>
public class ReverseGeocodingCacheReadTests
{
    private sealed record CacheRow(string Address, string? Road, DateTime ResolvedAt);

    [Fact]
    public async Task CachedAddress_ComesFromTheCache_WithoutAnyHttpCall()
    {
        var (service, http) = Create(new CacheRow("12 Mall Road, Lahore, Punjab", "Mall Road", DateTime.UtcNow.AddDays(-2)));

        var result = await service.GetCachedAddressAsync(31.5204, 74.3587);

        result.Should().NotBeNull();
        result!.FormattedAddress.Should().Be("12 Mall Road, Lahore, Punjab");
        result.FromCache.Should().BeTrue();
        http.Count.Should().Be(0);
    }

    [Fact]
    public async Task CachedAddress_ADistrictOnlyOrOldAnswer_IsStillShown_WhileTheProviderPathAsksAgain()
    {
        var old = new CacheRow("Pasrur Tehsil, Sialkot District, Punjab", null, DateTime.UtcNow.AddDays(-60));
        var (service, http) = Create(old);

        var shown = await service.GetCachedAddressAsync(32.2626, 74.6630);

        shown.Should().NotBeNull("a district name beats coordinates on screen");
        shown!.FormattedAddress.Should().Be(old.Address);
        http.Count.Should().Be(0);

        await service.GetAddressAsync(32.2626, 74.6630);
        http.Count.Should().BeGreaterThan(0, "the provider path still refreshes an old, district-only answer");
    }

    [Fact]
    public async Task CachedAddress_NothingCached_IsNull_WithoutAnyHttpCall()
    {
        var (service, http) = Create(row: null);

        (await service.GetCachedAddressAsync(31.5204, 74.3587)).Should().BeNull();
        http.Count.Should().Be(0);
    }

    [Fact]
    public async Task CachedAddress_CacheUnreadable_IsNull_WithoutAnyHttpCall()
    {
        var (service, http) = Create(row: null, failingDb: true);

        (await service.GetCachedAddressAsync(31.5204, 74.3587)).Should().BeNull();
        http.Count.Should().Be(0);
    }

    private static (NominatimReverseGeocodingService Service, List<string> Http) Create(CacheRow? row, bool failingDb = false)
    {
        var http = new List<string>();
        var handler = new RecordingHandler(req =>
        {
            http.Add(req.RequestUri!.Host);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"error":"Unable to geocode"}""", System.Text.Encoding.UTF8, "application/json")
            };
        });
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://nominatim.openstreetmap.org/") });
        var db = new Mock<IDbConnectionFactory>();
        db.Setup(f => f.CreateConnection()).Returns(() => new FakeCacheConnection(row, failingDb));

        var service = new NominatimReverseGeocodingService(
            db.Object,
            clients.Object,
            Options.Create(new GeocodingOptions { Enabled = true, BaseUrl = "https://nominatim.openstreetmap.org" }),
            NullLogger<NominatimReverseGeocodingService>.Instance);
        return (service, http);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    /// <summary>Answers the GpsAddressCache SELECT with <paramref name="row"/> (or nothing); every write succeeds.</summary>
    private sealed class FakeCacheConnection(CacheRow? row, bool failing) : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "FakeDb";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() =>
            failing ? throw new InvalidOperationException("test-no-db") : new FakeCacheCommand(this, row);
    }

    private sealed class FakeCacheCommand(DbConnection connection, CacheRow? row) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameters();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override void Prepare() { }
        public override int ExecuteNonQuery() => 1;
        public override object? ExecuteScalar() => null;
        protected override DbParameter CreateDbParameter() => new FakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var table = new DataTable();
            foreach (var column in new[] { "Address", "City", "State", "Country", "PostalCode", "Road", "PlaceName", "PlaceType" })
                table.Columns.Add(column, typeof(string));
            table.Columns.Add("ResolvedAt", typeof(DateTime));
            if (row is not null && (CommandText ?? "").Contains("FROM GpsAddressCache", StringComparison.OrdinalIgnoreCase))
                table.Rows.Add(row.Address, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, (object?)row.Road ?? DBNull.Value, DBNull.Value, DBNull.Value, row.ResolvedAt);
            return table.CreateDataReader();
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
        private readonly List<object> _items = [];
        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;
        public override int Add(object value) { _items.Add(value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (var v in values) _items.Add(v!); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains(value);
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf(value);
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) => _items.Insert(index, value);
        public override void Remove(object value) => _items.Remove(value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => (DbParameter)_items[index];
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotSupportedException();
    }
}
