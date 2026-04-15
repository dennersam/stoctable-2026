import type { UserRole } from './common';

export interface AuthUser {
  id: string;
  username: string;
  fullName: string;
  email: string;
  role: UserRole;
  branchIds: string[];
  avatarUrl?: string | null;
}

export interface AuthTokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface LoginRequest {
  username: string;
  password: string;
}
