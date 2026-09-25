using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Infrastructure.Services.WhatsApp;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppCloudApiServiceTests
{
    private static readonly WhatsAppAccountRow TestAccount = new(
        Id: 1,
        TenantId: 1,
        Code: "UAE",
        Name: "SheikhGo UAE",
        DisplayPhoneNumber: "+971557701219",
        Purpose: "Sales",
        PhoneNumberId: null,
        BusinessAccountId: null,
        IsActive: true);

    /// <summary>Fake Graph token used only to assert redaction — not a real credential.</summary>
    private const string GraphTokenFixture = "wa-unit-graph-token-fixture";

    [Fact]
    public async Task SendTextAsync_Success_ReturnsMetaMessageId()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            seen = req;
            return JsonResponse(HttpStatusCode.OK, """{"messages":[{"id":"wamid.SUCCESS1"}]}""");
        });

        var service = CreateService(handler, configured: true);
        var result = await service.SendTextAsync(TestAccount, "971501234567", "hello");

        result.Success.Should().BeTrue();
        result.MetaMessageId.Should().Be("wamid.SUCCESS1");
        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.None);
        seen.Should().NotBeNull();
        seen!.Method.Should().Be(HttpMethod.Post);
        seen.RequestUri!.ToString().Should().Contain("phone-uae-1/messages");
        seen.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", GraphTokenFixture));
    }

    [Fact]
    public async Task SendTextAsync_MetaError_MapsErrorKind()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.BadRequest, """
                {"error":{"message":"Invalid OAuth access token.","type":"OAuthException","code":190}}
                """));

        var service = CreateService(handler, configured: true);
        var result = await service.SendTextAsync(TestAccount, "971501234567", "hello");

        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.InvalidToken);
        result.ErrorCode.Should().Be("190");
        result.ErrorMessage.Should().Contain("OAuth");
        result.ErrorMessage.Should().NotContain(GraphTokenFixture);
    }

    [Fact]
    public async Task SendTextAsync_InvalidConfiguration_DoesNotCallHttp()
    {
        var called = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            called = true;
            return JsonResponse(HttpStatusCode.OK, """{"messages":[{"id":"x"}]}""");
        });

        var service = CreateService(handler, configured: false);
        var result = await service.SendTextAsync(TestAccount, "971501234567", "hello");

        called.Should().BeFalse();
        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.InvalidConfiguration);
    }

    [Fact]
    public async Task SendTextAsync_NetworkFailure_ReturnsNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection reset"));
        var service = CreateService(handler, configured: true);

        var result = await service.SendTextAsync(TestAccount, "971501234567", "hello");

        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.NetworkFailure);
    }

    [Fact]
    public async Task SendTextAsync_Timeout_ReturnsNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("timeout"));
        var service = CreateService(handler, configured: true);

        var result = await service.SendTextAsync(TestAccount, "971501234567", "hello");

        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.NetworkFailure);
    }

    [Fact]
    public async Task SendTemplateAsync_Success()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"messages":[{"id":"wamid.TMPL"}]}"""));
        var service = CreateService(handler, configured: true);

        var result = await service.SendTemplateAsync(
            TestAccount, "971501234567", "hello_world", "en_US", ["Ali"]);

        result.Success.Should().BeTrue();
        result.MetaMessageId.Should().Be("wamid.TMPL");
    }

    [Fact]
    public async Task MarkMessageReadAsync_Success_WithoutMessageId()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"success":true}"""));
        var service = CreateService(handler, configured: true);

        var result = await service.MarkMessageReadAsync(TestAccount, "wamid.INBOUND1");

        result.Success.Should().BeTrue();
        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.None);
    }

    [Fact]
    public async Task SendTextAsync_RateLimited_MapsKind()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse((HttpStatusCode)429, """{"error":{"message":"Rate limit hit","code":130429}}"""));
        var service = CreateService(handler, configured: true);

        var result = await service.SendTextAsync(TestAccount, "971501234567", "hello");

        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.RateLimited);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "190", "bad token", WhatsAppCloudErrorKind.InvalidToken)]
    [InlineData(HttpStatusCode.NotFound, "100", "phone number id", WhatsAppCloudErrorKind.InvalidPhoneNumberId)]
    [InlineData(HttpStatusCode.BadRequest, "132001", "Template name does not exist", WhatsAppCloudErrorKind.TemplateError)]
    [InlineData(HttpStatusCode.BadRequest, "131026", "Message undeliverable", WhatsAppCloudErrorKind.InvalidRecipient)]
    public void ClassifyError_MapsKnownCases(
        HttpStatusCode status, string code, string message, WhatsAppCloudErrorKind expected)
    {
        WhatsAppCloudApiService.ClassifyError(status, code, message, null).Should().Be(expected);
    }

    [Fact]
    public async Task SendTextAsync_DoesNotLogAccessToken()
    {
        var logger = new CapturingLogger();
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.BadRequest,
                "{\"error\":{\"message\":\"fail " + GraphTokenFixture + " leak\",\"code\":1}}"));

        var service = CreateService(handler, configured: true, logger);
        await service.SendTextAsync(TestAccount, "971501234567", "hello");

        string.Join('\n', logger.Messages).Should().NotContain(GraphTokenFixture);
    }

    [Fact]
    public async Task CheckPhoneNumberAsync_Success_ReturnsOk()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            seen = req;
            return JsonResponse(HttpStatusCode.OK,
                """{"id":"phone-uae-1","display_phone_number":"+971 55 770 1219","verified_name":"SheikhGo"}""");
        });

        var service = CreateService(handler, configured: true);
        var result = await service.CheckPhoneNumberAsync(TestAccount);

        result.Success.Should().BeTrue();
        result.MetaMessageId.Should().Be("phone-uae-1");
        seen!.Method.Should().Be(HttpMethod.Get);
        seen.RequestUri!.ToString().Should().Contain("phone-uae-1");
        seen.Headers.Authorization!.Parameter.Should().Be(GraphTokenFixture);
    }

    [Fact]
    public async Task CheckPhoneNumberAsync_MissingConfig_DoesNotHttp()
    {
        var called = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            called = true;
            return JsonResponse(HttpStatusCode.OK, "{}");
        });

        var service = CreateService(handler, configured: false);
        var result = await service.CheckPhoneNumberAsync(TestAccount);

        called.Should().BeFalse();
        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(WhatsAppCloudErrorKind.InvalidConfiguration);
    }

    private static WhatsAppCloudApiService CreateService(
        HttpMessageHandler handler,
        bool configured,
        ILogger<WhatsAppCloudApiService>? logger = null)
    {
        var options = Options.Create(new WhatsAppOptions
        {
            GraphApiVersion = "v21.0",
            Uae = configured
                ? new WhatsAppAccountOptions
                {
                    PhoneNumberId = "phone-uae-1",
                    AccessToken = GraphTokenFixture,
                    BusinessAccountId = "waba-1"
                }
                : new WhatsAppAccountOptions()
        });

        var accountConfig = new WhatsAppAccountConfig(options);
        var factory = new NamedHttpClientFactory(new Dictionary<string, HttpMessageHandler>
        {
            [WhatsAppCloudApiService.HttpClientName] = handler
        });

        return new WhatsAppCloudApiService(
            factory,
            accountConfig,
            logger ?? NullLogger<WhatsAppCloudApiService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
        => new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class NamedHttpClientFactory(Dictionary<string, HttpMessageHandler> handlers) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            if (!handlers.TryGetValue(name, out var handler))
                throw new InvalidOperationException($"No handler for {name}");
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://graph.facebook.com/v21.0/")
            };
        }
    }

    private sealed class CapturingLogger : ILogger<WhatsAppCloudApiService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }
}
