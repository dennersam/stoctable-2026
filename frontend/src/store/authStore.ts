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
    localStorage.setItem('authUser', JSON.stringify(user));
    set({ user, accessToken, isAuthenticated: true });
  },

  clearAuth: () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('authUser');
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
  const storedUser = localStorage.getItem('authUser');
  if (!token || !storedUser) return;

  try {
    // Check token expiry without relying on claim names
    const payload = JSON.parse(atob(token.split('.')[1]));
    const now = Math.floor(Date.now() / 1000);
    if (payload.exp && payload.exp < now) return;

    const user: AuthUser = JSON.parse(storedUser);
    useAuthStore.getState().setAuth(user, token, localStorage.getItem('refreshToken') ?? '');
  } catch {
    // Invalid token or corrupted stored user — ignore
  }
}
