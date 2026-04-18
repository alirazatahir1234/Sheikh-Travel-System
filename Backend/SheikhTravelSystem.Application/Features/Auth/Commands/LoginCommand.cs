using MediatR;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<ApiResponse<LoginResponse>>;

public record LoginResponse(string AccessToken, string RefreshToken, string FullName, string Role);
