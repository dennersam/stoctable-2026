import api from '@/lib/api';
import type { AuthTokenResponse, LoginRequest } from '@/types/auth';

export const authService = {
  login: async (data: LoginRequest): Promise<AuthTokenResponse> => {
    const response = await api.post<AuthTokenResponse>('/auth/login', data);
    return response.data;
  },

  /**
   * A filial ativa vai junto: sem ela, uma conta com várias lojas seria
   * devolvida à tela de escolha a cada renovação de token. O servidor revalida
   * a permissão contra o banco antes de devolvê-la.
   */
  refresh: async (refreshToken: string, branchId?: string | null): Promise<AuthTokenResponse> => {
    const response = await api.post<AuthTokenResponse>('/auth/refresh', { refreshToken, branchId });
    return response.data;
  },

  /** Troca o token atual por um amarrado à filial escolhida. */
  selectBranch: async (branchId: string): Promise<AuthTokenResponse> => {
    const response = await api.post<AuthTokenResponse>('/auth/select-branch', { branchId });
    return response.data;
  },

  logout: async (): Promise<void> => {
    await api.post('/auth/logout');
  },

  forgotPassword: async (email: string): Promise<void> => {
    await api.post('/auth/forgot-password', { email });
  },

  validateResetToken: async (token: string): Promise<void> => {
    await api.get('/auth/reset-password/validate', { params: { token } });
  },

  resetPassword: async (token: string, newPassword: string): Promise<void> => {
    await api.post('/auth/reset-password', { token, newPassword });
  },
};
