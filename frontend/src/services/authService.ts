import api from '@/lib/api';
import type { AuthTokenResponse, LoginRequest } from '@/types/auth';

export const authService = {
  login: async (data: LoginRequest): Promise<AuthTokenResponse> => {
    const response = await api.post<AuthTokenResponse>('/auth/login', data);
    return response.data;
  },

  refresh: async (refreshToken: string): Promise<AuthTokenResponse> => {
    const response = await api.post<AuthTokenResponse>('/auth/refresh', { refreshToken });
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
