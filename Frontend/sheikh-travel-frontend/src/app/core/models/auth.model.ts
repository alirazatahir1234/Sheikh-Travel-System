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
  email?: string;
  phoneNumber?: string;
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
  userId?: number;
  email?: string;
  phoneNumber?: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}
