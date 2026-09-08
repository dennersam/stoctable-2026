import { create } from 'zustand';
import type { Branch } from '@/types/auth';
import { useCartStore } from '@/store/cartStore';

const STORAGE_KEY = 'stoctable-branches';

interface StoredBranchState {
  branches: Branch[];
  activeBranchId: string | null;
}

interface BranchState extends StoredBranchState {
  /** Filial ativa resolvida, ou null enquanto a escolha não foi feita. */
  active: () => Branch | null;
  setBranches: (branches: Branch[], activeBranchId: string | null) => void;
  clearBranch: () => void;
}

function read(): StoredBranchState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return JSON.parse(raw) as StoredBranchState;
  } catch {
    // Storage corrompido ou indisponível — começa vazio.
  }
  return { branches: [], activeBranchId: null };
}

function write(state: StoredBranchState) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Modo privativo ou storage cheio: a filial continua no token, então a
    // sessão funciona; só não sobrevive a um refresh de página.
  }
}

export const useBranchStore = create<BranchState>((set, get) => ({
  ...read(),

  active: () => {
    const { branches, activeBranchId } = get();
    return branches.find((b) => b.id === activeBranchId) ?? null;
  },

  setBranches: (branches, activeBranchId) => {
    const anterior = get().activeBranchId;

    // O carrinho guarda um orçamento em andamento, que é dado de filial:
    // estoque reservado e numeração pertencem à loja onde foi aberto. Mudou a
    // loja, o rascunho não vale mais.
    if (anterior && activeBranchId && anterior !== activeBranchId) {
      useCartStore.getState().clearCart();
    }

    write({ branches, activeBranchId });
    set({ branches, activeBranchId });
  },

  clearBranch: () => {
    localStorage.removeItem(STORAGE_KEY);
    set({ branches: [], activeBranchId: null });
  },
}));
