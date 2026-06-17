import type { UserRole } from './common';

export interface SystemUser {
  id: string;
  username: string;
  email: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  avatarUrl?: string | null;
  lastLoginAt?: string | null;
  createdAt: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  fullName: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  fullName?: string;
  email?: string;
  role?: UserRole;
  isActive?: boolean;
}
