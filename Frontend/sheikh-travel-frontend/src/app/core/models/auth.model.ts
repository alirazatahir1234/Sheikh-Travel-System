export interface LoginRequest {
  email: string;
  password: string;
}

/**
 * Raw shape returned inside `ApiResponse.data` from POST /api/auth/login.
 * Mirrors SheikhTravelSystem.Application.Features.Auth.Commands.LoginResponse.
 */
export interface LoginResponseDto {
  accessToken: string;
  refreshToken: string;
  fullName: string;
  role: string;
}

/**
 * Normalized user session used by the frontend.
 * We expose `roles[]` (array) for consistency even though the backend
 * sends a single `role` string, so components can uniformly do `roles.includes(...)`.
 */
export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  fullName: string;
  roles: string[];
  /** Optional — only present if the backend grows richer claims later. */
  userId?: number;
  email?: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}
