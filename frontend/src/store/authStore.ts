import { create } from 'zustand';
import type { AuthUser, Company } from '@/types/auth';
import type { UserRole } from '@/types/common';

interface AuthState {
  user: AuthUser | null;
  company: Company | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  setAuth: (user: AuthUser, accessToken: string, refreshToken: string, company?: Company | null) => void;
  clearAuth: () => void;
  hasRole: (...roles: UserRole[]) => boolean;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  company: null,
  accessToken: null,
  isAuthenticated: false,

  setAuth: (user, accessToken, refreshToken, company = null) => {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    localStorage.setItem('authUser', JSON.stringify(user));
    if (company) localStorage.setItem('authCompany', JSON.stringify(company));
    set({ user, company: company ?? get().company, accessToken, isAuthenticated: true });
  },

  clearAuth: () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('authUser');
    localStorage.removeItem('authCompany');
    localStorage.removeItem('stoctable-branches');
    // Resquício do modelo antigo, em que a filial era um header escolhido pelo
    // cliente. Removido também na limpeza para não confundir quem inspecionar
    // o storage.
    localStorage.removeItem('branchId');
    localStorage.removeItem('branchName');
    set({ user: null, company: null, accessToken: null, isAuthenticated: false });
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
    const storedCompany = localStorage.getItem('authCompany');
    const company: Company | null = storedCompany ? JSON.parse(storedCompany) : null;

    useAuthStore
      .getState()
      .setAuth(user, token, localStorage.getItem('refreshToken') ?? '', company);
  } catch {
    // Invalid token or corrupted stored user — ignore
  }
}

/**
 * Lê a filial ativa direto do token, sem confiar no que está guardado.
 *
 * A filial passou a ser uma claim assinada: se o storage disser uma coisa e o
 * token disser outra, quem manda é o token — o servidor vai obedecer ao token
 * de qualquer forma.
 */
export function branchIdFromToken(): string | null {
  const token = localStorage.getItem('accessToken');
  if (!token) return null;

  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload.branch_id ?? null;
  } catch {
    return null;
  }
}
