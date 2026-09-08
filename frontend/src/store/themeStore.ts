import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface ThemeState {
  isDark: boolean;
  toggle: () => void;
}

/**
 * Aplica a classe `.dark` no elemento raiz do documento.
 *
 * Antes ela era aplicada dentro do Layout, o que amarrava o tema ao shell da
 * aplicação: qualquer página fora dele — login e agora o portal público —
 * ficava sempre no claro. No <html> vale para o documento inteiro, e de quebra
 * some o flash de tema errado no primeiro render.
 */
function applyTheme(isDark: boolean) {
  document.documentElement.classList.toggle('dark', isDark);
}

export const useThemeStore = create<ThemeState>()(
  persist(
    (set) => ({
      isDark: false,
      toggle: () =>
        set((s) => {
          const isDark = !s.isDark;
          applyTheme(isDark);
          return { isDark };
        }),
    }),
    {
      name: 'stoctable-theme',
      // Roda depois da leitura do localStorage: sem isto, um usuário que
      // escolheu o escuro veria o claro até o primeiro toggle.
      onRehydrateStorage: () => (state) => applyTheme(state?.isDark ?? false),
    }
  )
);

/** Chamado no main.tsx, antes do primeiro render. */
export function hydrateTheme() {
  applyTheme(useThemeStore.getState().isDark);
}
