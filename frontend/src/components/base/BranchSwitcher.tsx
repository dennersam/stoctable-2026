import { useState } from 'react';
import { Building2, Check, ChevronDown } from 'lucide-react';
import toast from 'react-hot-toast';
import { authService } from '@/services/authService';
import { applySession } from '@/lib/session';
import { useBranchStore } from '@/store/branchStore';
import { useCartStore } from '@/store/cartStore';
import { cn } from '@/lib/utils';

/**
 * Troca de filial dentro do sistema.
 *
 * A troca NÃO é um flip de estado no cliente: ela pede um token novo ao
 * servidor e recarrega a página. Parece exagero, mas é o único jeito correto
 * aqui — não existe react-query nem qualquer invalidação de cache, as páginas
 * guardam os dados do servidor em useEffect + useState, e várias nem têm a
 * filial nas dependências do efeito. Numa troca suave, metade da tela
 * continuaria mostrando os números da loja anterior por tempo indefinido.
 * Recarregar custa uns 300ms e é correto por construção.
 *
 * Some da interface quando a conta só tem uma loja, que é o caso comum.
 */
export function BranchSwitcher() {
  const { branches, activeBranchId } = useBranchStore();
  const cartItems = useCartStore((s) => s.items);

  const [open, setOpen] = useState(false);
  const [switching, setSwitching] = useState(false);

  if (branches.length <= 1) return null;

  const active = branches.find((b) => b.id === activeBranchId);

  const handleSwitch = async (branchId: string) => {
    if (branchId === activeBranchId) {
      setOpen(false);
      return;
    }

    // O carrinho é um orçamento em andamento naquela loja: estoque reservado e
    // numeração pertencem a ela. Trocar descarta, então avisa antes.
    if (cartItems.length > 0) {
      const confirmar = window.confirm(
        'Você tem um orçamento em andamento. Trocar de filial vai descartá-lo. Continuar?'
      );
      if (!confirmar) return;
    }

    setSwitching(true);
    try {
      applySession(await authService.selectBranch(branchId));
      // Navegação dura de propósito — ver o comentário no topo do arquivo.
      window.location.assign('/dashboard');
    } catch {
      toast.error('Não foi possível trocar de filial.');
      setSwitching(false);
      setOpen(false);
    }
  };

  return (
    <div className="relative">
      <button
        type="button"
        disabled={switching}
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="listbox"
        aria-expanded={open}
        className={cn(
          'flex items-center gap-2 rounded-md px-2.5 py-1.5 text-sm transition-colors',
          'text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-white/10',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500',
          'disabled:opacity-60'
        )}
      >
        <Building2 size={16} className="shrink-0" />
        <span className="max-w-[10rem] truncate font-medium">{active?.name ?? 'Selecionar filial'}</span>
        <ChevronDown size={14} className={cn('shrink-0 transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} aria-hidden />

          <ul
            role="listbox"
            className="absolute left-0 z-50 mt-1 w-64 overflow-hidden rounded-lg border border-gray-200 bg-white py-1 shadow-lg dark:border-white/10 dark:bg-zinc-800"
          >
            {branches.map((branch) => (
              <li key={branch.id}>
                <button
                  type="button"
                  role="option"
                  aria-selected={branch.id === activeBranchId}
                  onClick={() => handleSwitch(branch.id)}
                  className="flex w-full items-center gap-3 px-3 py-2 text-left text-sm transition-colors hover:bg-gray-100 dark:hover:bg-white/10"
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <span className="truncate font-medium text-gray-900 dark:text-white">
                        {branch.name}
                      </span>
                      {branch.isHeadquarters && (
                        <span className="shrink-0 rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-600 dark:bg-white/10 dark:text-gray-300">
                          Matriz
                        </span>
                      )}
                    </div>
                    <span className="text-xs text-gray-500 dark:text-gray-400">{branch.code}</span>
                  </div>

                  {branch.id === activeBranchId && (
                    <Check size={16} className="shrink-0 text-brand-600 dark:text-brand-300" />
                  )}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
