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

export interface Company {
  id: string;
  razaoSocial: string;
  nomeFantasia?: string | null;
  cnpj: string;
}

export interface Branch {
  id: string;
  code: string;
  name: string;
  cnpj?: string | null;
  isHeadquarters: boolean;
}

export interface AuthTokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
  company: Company;
  branches: Branch[];
  /**
   * Verdadeiro quando o token é o de PRÉ-FILIAL: a conta tem acesso a mais de
   * uma loja e ainda não escolheu. Esse token só abre /auth/select-branch —
   * nenhum endpoint de negócio o aceita.
   */
  requiresBranchSelection: boolean;
  activeBranchId: string | null;
}

export interface LoginRequest {
  /** É o e-mail: virou a identidade de login do SaaS. O nome do campo ficou por compatibilidade. */
  username: string;
  password: string;
}
