import type { AuthTokenResponse } from '@/types/auth';
import { useAuthStore } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';

/**
 * Ponto único que aplica a resposta de autenticação nos stores.
 *
 * Login, escolha de filial, troca de filial e renovação de token devolvem
 * exatamente a mesma coisa, e todos precisam atualizar token, empresa, lista
 * de filiais e filial ativa em conjunto. Espalhar isso por quatro telas é como
 * se produz uma sessão em que o token diz uma loja e a interface mostra outra.
 */
export function applySession(response: AuthTokenResponse) {
  useAuthStore.getState().setAuth(
    response.user,
    response.accessToken,
    response.refreshToken,
    response.company
  );

  useBranchStore.getState().setBranches(response.branches, response.activeBranchId);
}
