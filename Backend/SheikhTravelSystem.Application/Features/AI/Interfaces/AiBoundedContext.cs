namespace SheikhTravelSystem.Application.Features.AI.Interfaces;

/// <summary>
/// AI Application bounded context marker.
/// Provider-agnostic ports live in <c>Application.Common.Interfaces</c>
/// (<see cref="Common.Interfaces.IAiProvider"/>, <see cref="Common.Interfaces.IAiChatGateway"/>,
/// <see cref="Common.Interfaces.IAiPredictionService"/>, etc.).
/// Concrete Ollama/OpenAI/Azure adapters stay in Infrastructure/Services/Ai.
/// Domain entities (Vehicle, Driver, Booking, …) are dependencies — never moved into AI.
/// </summary>
public static class AiBoundedContext
{
    public const string Name = "AI";
    public const string DefaultIntent = "AI Operations";
}
