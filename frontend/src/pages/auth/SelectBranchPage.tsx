import { useEffect, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { Building2, Check, LogOut } from 'lucide-react';
import { authService } from '@/services/authService';
import { applySession } from '@/lib/session';
import { useAuthStore } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';
import { AuthError, AuthShell } from '@/components/base/AuthShell';
import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';

/**
 * Escolha da loja, mostrada quando a conta tem acesso a mais de uma.
 *
 * Não é uma preferência de interface: até escolher, a sessão carrega um token
 * de pré-filial que nenhum endpoint de negócio aceita. Escolher é o que troca
 * esse token por um de sessão.
 */
export function SelectBranchPage() {
  const navigate = useNavigate();
  const { isAuthenticated, user, company, clearAuth } = useAuthStore();
  const { branches, activeBranchId } = useBranchStore();

  const [selecting, setSelecting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    document.title = 'Escolha a filial — Stoctable';
  }, []);

  if (!isAuthenticated) return <Navigate to="/login" replace />;

  // Já há filial ativa (ou só existe uma): não há o que escolher.
  if (activeBranchId || branches.length <= 1) return <Navigate to="/dashboard" replace />;

  const handleSelect = async (branchId: string) => {
    setError(null);
    setSelecting(branchId);

    try {
      applySession(await authService.selectBranch(branchId));
      navigate('/dashboard', { replace: true });
    } catch (err) {
      if (isAxiosError(err) && err.response?.status === 401) {
        setError('Você não tem acesso a esta filial. Entre novamente.');
      } else {
        setError('Não foi possível entrar nesta filial. Tente novamente.');
      }
      setSelecting(null);
    }
  };

  const handleLogout = () => {
    clearAuth();
    navigate('/login', { replace: true });
  };

  return (
    <AuthShell
      wide
      subtitle={
        <>
          <h1 className="text-lg font-semibold text-gray-900 dark:text-white">Escolha a filial</h1>
          <p className="mt-1">
            Olá, {user?.fullName}. {company?.nomeFantasia ?? company?.razaoSocial} tem mais de uma
            loja. Selecione em qual você vai trabalhar agora.
          </p>
        </>
      }
    >
      {error && <div className="mb-4"><AuthError>{error}</AuthError></div>}

      <ul className="space-y-2">
        {branches.map((branch) => (
          <li key={branch.id}>
            <button
              type="button"
              disabled={selecting !== null}
              onClick={() => handleSelect(branch.id)}
              className={cn(
                'flex min-h-16 w-full items-center gap-4 rounded-xl border border-line bg-surface p-4 text-left transition-colors',
                'hover:border-brand-300 dark:hover:border-brand-300/50',
                'disabled:cursor-not-allowed disabled:opacity-60'
              )}
            >
              <Building2 size={20} className="shrink-0 text-gray-500 dark:text-gray-400" />

              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <span className="truncate font-medium text-gray-900 dark:text-white">{branch.name}</span>
                  {branch.isHeadquarters && (
                    <span className="shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700 dark:bg-white/10 dark:text-gray-200">
                      Matriz
                    </span>
                  )}
                </div>
                <p className="mt-0.5 truncate text-sm text-gray-500 dark:text-gray-400">
                  {branch.code}
                  {branch.cnpj ? ' · ' + formatCnpj(branch.cnpj) : ''}
                </p>
              </div>

              {selecting === branch.id && <Check size={18} className="shrink-0 text-brand-600 dark:text-brand-300" />}
            </button>
          </li>
        ))}
      </ul>

      <div className="mt-6 text-center">
        <Button variant="ghost" size="sm" onClick={handleLogout}>
          <LogOut size={16} />
          Sair
        </Button>
      </div>
    </AuthShell>
  );
}

function formatCnpj(cnpj: string) {
  if (cnpj.length !== 14) return cnpj;
  return `${cnpj.slice(0, 2)}.${cnpj.slice(2, 5)}.${cnpj.slice(5, 8)}/${cnpj.slice(8, 12)}-${cnpj.slice(12)}`;
}
