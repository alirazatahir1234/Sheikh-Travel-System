export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  userId: number;
  fullName: string;
  email: string;
  roles: string[];
}

export interface RefreshTokenRequest {
  refreshToken: string;
}
