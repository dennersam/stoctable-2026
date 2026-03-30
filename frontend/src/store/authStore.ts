import { create } from 'zustand';
import type { AuthUser } from '@/types/auth';
import type { UserRole } from '@/types/common';

interface AuthState {
  user: AuthUser | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  setAuth: (user: AuthUser, accessToken: string, refreshToken: string) => void;
  clearAuth: () => void;
  hasRole: (...roles: UserRole[]) => boolean;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: null,
  isAuthenticated: false,

  setAuth: (user, accessToken, refreshToken) => {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    set({ user, accessToken, isAuthenticated: true });
  },

  clearAuth: () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('branchId');
    set({ user: null, accessToken: null, isAuthenticated: false });
  },

  hasRole: (...roles) => {
    const { user } = get();
    if (!user) return false;
    return roles.includes(user.role);
  },
}));

// Hydrate from localStorage on app start
export function hydrateAuth() {
  const token = localStorage.getItem('accessToken');
  if (!token) return;

  try {
    // Decode JWT payload to get user info (no library needed for payload decode)
    const payload = JSON.parse(atob(token.split('.')[1]));
    const now = Math.floor(Date.now() / 1000);

    if (payload.exp && payload.exp < now) {
      // Token expired — clear and let interceptor handle refresh
      return;
    }

    const user: AuthUser = {
      id: payload.sub,
      username: payload.preferred_username ?? payload.sub,
      fullName: payload.name ?? '',
      email: payload.email ?? '',
      role: payload.role,
      branchIds: JSON.parse(payload.branch_ids ?? '[]'),
    };

    useAuthStore.getState().setAuth(
      user,
      token,
      localStorage.getItem('refreshToken') ?? ''
    );
  } catch {
    // Invalid token — ignore
  }
}
