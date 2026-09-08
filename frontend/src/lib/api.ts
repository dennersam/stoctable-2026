import axios, { type AxiosError } from 'axios';
import { env } from '@/config/env';

const api = axios.create({
  baseURL: `${env.apiBaseUrl}/api`,
  headers: {
    'Content-Type': 'application/json',
    'X-Branch-Id': 'dev'
  },
});

// Request interceptor: attach JWT + branch ID
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const branchId = localStorage.getItem('branchId');
  if (branchId) {
    config.headers['X-Branch-Id'] = branchId;
  }

  return config;
});

// Endpoints de autenticação: um 401 aqui é credencial inválida, não sessão
// expirada — deve chegar na tela para ser exibido, sem refresh nem redirect.
const AUTH_PATHS = [
  '/auth/login',
  '/auth/refresh',
  '/auth/forgot-password',
  '/auth/reset-password',
];

function isAuthRequest(url?: string) {
  if (!url) return false;
  return AUTH_PATHS.some((path) => url.includes(path));
}

// Response interceptor: handle 401 with token refresh
api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as typeof error.config & { _retry?: boolean };

    if (isAuthRequest(originalRequest?.url)) {
      return Promise.reject(error);
    }

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      const refreshToken = localStorage.getItem('refreshToken');
      if (!refreshToken) {
        redirectToLogin();
        return Promise.reject(error);
      }

      try {
        const response = await axios.post(`${env.apiBaseUrl}/api/auth/refresh`, {
          refreshToken,
        });

        const { accessToken, refreshToken: newRefreshToken } = response.data;
        localStorage.setItem('accessToken', accessToken);
        localStorage.setItem('refreshToken', newRefreshToken);

        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        }

        return api(originalRequest);
      } catch {
        redirectToLogin();
        return Promise.reject(error);
      }
    }

    return Promise.reject(error);
  }
);

function clearAuth() {
  localStorage.removeItem('accessToken');
  localStorage.removeItem('refreshToken');
  localStorage.removeItem('authUser');
  localStorage.removeItem('branchId');
}

// Rotas públicas: o visitante não pediu para entrar em lugar nenhum, então uma
// sessão expirada limpa o storage mas NÃO arranca ele da página. Sem esta
// guarda, um refresh token velho jogaria quem está lendo a landing page direto
// num formulário de login.
const PUBLIC_PATHS = ['/', '/cadastro', '/login', '/forgot-password', '/reset-password'];

function isOnPublicPage() {
  const path = window.location.pathname;
  return PUBLIC_PATHS.some((p) => path === p || path.startsWith(`${p}/`));
}

function redirectToLogin() {
  clearAuth();
  if (isOnPublicPage()) return;
  window.location.href = '/login';
}

export default api;
