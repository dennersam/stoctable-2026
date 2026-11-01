import { create } from 'zustand';

export interface DialogOptions {
  title: string;
  description?: string;
  /** Diz o que acontece: "Excluir produto", não "OK". */
  confirmLabel: string;
  cancelLabel?: string;
  tone?: 'default' | 'danger';
  /** Quando presente, o diálogo pede um texto antes de confirmar. */
  input?: { label: string; placeholder?: string; required?: boolean };
}

interface PendingDialog extends DialogOptions {
  resolve: (result: string | null) => void;
}

interface DialogState {
  pending: PendingDialog | null;
  settle: (result: string | null) => void;
}

export const useDialogStore = create<DialogState>((set, get) => ({
  pending: null,
  settle: (result) => {
    get().pending?.resolve(result);
    set({ pending: null });
  },
}));

function open(options: DialogOptions) {
  return new Promise<string | null>((resolve) => {
    // Um diálogo novo nunca deixa o anterior pendurado sem resposta.
    useDialogStore.getState().pending?.resolve(null);
    useDialogStore.setState({ pending: { ...options, resolve } });
  });
}

/** Substitui o confirm() nativo. Resolve true se a pessoa confirmou. */
export async function confirmDialog(options: Omit<DialogOptions, 'input'>) {
  return (await open(options)) !== null;
}

/** Substitui o prompt() nativo. Resolve com o texto, ou null se cancelou. */
export function promptDialog(options: DialogOptions & { input: NonNullable<DialogOptions['input']> }) {
  return open(options);
}
