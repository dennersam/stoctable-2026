import { useCallback, useEffect, useState } from 'react';
import { useBranchStore } from '@/store/branchStore';

interface LoadState<T> {
  /** Requisição que produziu este resultado. */
  key: string | null;
  data: T | null;
  failed: boolean;
}

/**
 * Carrega um painel e recarrega ao trocar de filial. `loading` é derivado de a
 * chave atual ainda não ter resultado, e assim os números antigos continuam na
 * tela durante a troca, sem salto de layout.
 */
export function useDashboardData<T>(load: () => Promise<T>) {
  const branchId = useBranchStore((s) => s.activeBranchId);
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<LoadState<T>>({ key: null, data: null, failed: false });
  const key = `${branchId}:${attempt}`;

  useEffect(() => {
    let active = true;
    load()
      .then((data) => {
        if (active) setState({ key, data, failed: false });
      })
      .catch(() => {
        if (active) setState((current) => ({ key, data: current.data, failed: true }));
      });
    return () => {
      active = false;
    };
  }, [key, load]);

  const retry = useCallback(() => setAttempt((n) => n + 1), []);

  return { data: state.data, loading: state.key !== key, failed: state.failed, retry };
}
