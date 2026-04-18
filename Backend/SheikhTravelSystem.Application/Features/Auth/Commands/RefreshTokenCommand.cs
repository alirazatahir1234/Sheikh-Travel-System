using MediatR;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<ApiResponse<LoginResponse>>;
