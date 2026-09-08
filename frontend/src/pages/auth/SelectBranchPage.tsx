import { useEffect, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { Building2, Check, LogOut } from 'lucide-react';
import { authService } from '@/services/authService';
import { applySession } from '@/lib/session';
import { useAuthStore } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';
import { Logo } from '@/components/base/Logo';
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
    <div className="flex min-h-screen items-center justify-center bg-brand-950 px-4 py-12">
      <div className="w-full max-w-lg">
        <div className="mb-8 flex flex-col items-center text-center">
          <Logo size={40} className="text-white" />
          <h1 className="mt-4 text-2xl font-semibold text-white">Escolha a filial</h1>
          <p className="mt-2 text-sm text-brand-200">
            Olá, {user?.fullName}. {company?.nomeFantasia ?? company?.razaoSocial} tem mais de uma
            loja — selecione em qual você vai trabalhar agora.
          </p>
        </div>

        {error && (
          <div className="mb-4 rounded-md border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-200">
            {error}
          </div>
        )}

        <ul className="space-y-3">
          {branches.map((branch) => (
            <li key={branch.id}>
              <button
                type="button"
                disabled={selecting !== null}
                onClick={() => handleSelect(branch.id)}
                className={cn(
                  'flex w-full items-center gap-4 rounded-xl border border-brand-800 bg-brand-900 p-4 text-left transition-colors',
                  'hover:border-brand-500 hover:bg-brand-800',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-400',
                  'disabled:cursor-not-allowed disabled:opacity-60'
                )}
              >
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-brand-800 text-brand-200">
                  <Building2 size={20} />
                </div>

                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="truncate font-medium text-white">{branch.name}</span>
                    {branch.isHeadquarters && (
                      <span className="shrink-0 rounded-full bg-brand-700 px-2 py-0.5 text-xs text-brand-100">
                        Matriz
                      </span>
                    )}
                  </div>
                  <p className="mt-0.5 truncate text-sm text-brand-300">
                    {branch.code}
                    {branch.cnpj ? ` · ${formatCnpj(branch.cnpj)}` : ''}
                  </p>
                </div>

                {selecting === branch.id && <Check size={18} className="shrink-0 text-brand-300" />}
              </button>
            </li>
          ))}
        </ul>

        <div className="mt-8 text-center">
          <Button variant="ghost" size="sm" onClick={handleLogout} className="text-brand-300 hover:bg-white/10">
            <LogOut size={16} />
            Sair
          </Button>
        </div>
      </div>
    </div>
  );
}

function formatCnpj(cnpj: string) {
  if (cnpj.length !== 14) return cnpj;
  return `${cnpj.slice(0, 2)}.${cnpj.slice(2, 5)}.${cnpj.slice(5, 8)}/${cnpj.slice(8, 12)}-${cnpj.slice(12)}`;
}
