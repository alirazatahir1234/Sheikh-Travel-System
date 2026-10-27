using SheikhTravelSystem.Application.Features.DriverApp.Services;

namespace SheikhTravelSystem.Tests.DriverApp;

public class DriverVoiceIntentParserTests
{
    [Theory]
    [InlineData("I need help emergency", null, DriverVoiceIntent.Sos)]
    [InlineData("تأخير بسبب الازدحام", null, DriverVoiceIntent.Alert)]
    [InlineData("Ankunft in 10 Minuten", null, DriverVoiceIntent.Message)]
    [InlineData("Panne am Reifen", null, DriverVoiceIntent.Alert)]
    [InlineData("hello dispatch", "message", DriverVoiceIntent.Message)]
    [InlineData("anything", "sos", DriverVoiceIntent.Sos)]
    public void Parse_maps_keywords_and_explicit_intent(
        string transcript, string? requested, DriverVoiceIntent expected)
    {
        Assert.Equal(expected, DriverVoiceIntentParser.Parse(transcript, requested));
    }
}
