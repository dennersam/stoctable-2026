import { Navigate } from 'react-router-dom';
import { useAuthStore, branchIdFromToken } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';
import type { UserRole } from '@/types/common';

interface ProtectedRouteProps {
  children: React.ReactNode;
  roles?: UserRole[];
}

export function ProtectedRoute({ children, roles }: ProtectedRouteProps) {
  const { isAuthenticated, hasRole } = useAuthStore();
  const branches = useBranchStore((s) => s.branches);

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  // Autenticado mas sem filial no token: é o token de pré-filial, que nenhum
  // endpoint de negócio aceita. Mandar para a escolha aqui evita que a pessoa
  // veja uma tela inteira de erros 403.
  //
  // A verificação é feita no TOKEN e não no storage: se os dois divergirem,
  // quem vale é o token — é a ele que o servidor obedece.
  if (branchIdFromToken() === null && branches.length > 1) {
    return <Navigate to="/select-branch" replace />;
  }

  if (roles && roles.length > 0 && !hasRole(...roles)) {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
}
