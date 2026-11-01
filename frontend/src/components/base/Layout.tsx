import { useState, useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import * as Dialog from '@radix-ui/react-dialog';
import { BottomNav } from './BottomNav';
import { ErrorBoundary } from './ErrorBoundary';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

// Telas com a própria barra de ação no rodapé: duas barras empilhadas sobrariam
// pouco espaço para o conteúdo, e aqui a pessoa está no meio de uma tarefa.
const HIDE_BOTTOM_NAV = [/^\/quotations\/new$/, /^\/checkout\/[^/]+$/];

export function Layout() {
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  const showBottomNav = !HIDE_BOTTOM_NAV.some((route) => route.test(location.pathname));

  // A classe .dark agora vive no <html> (ver themeStore): fora daqui, para que
  // login e portal público também respeitem o tema escolhido.
  return (
    <div className="flex h-dvh">
      <div className="flex h-dvh w-full bg-page p-2 gap-2">

        {/* Sidebar desktop — hidden below md */}
        <div className="hidden md:block h-full">
          <Sidebar />
        </div>

        {/* Sidebar mobile — drawer overlay */}
        <Dialog.Root open={mobileOpen} onOpenChange={setMobileOpen}>
          <Dialog.Portal>
            <Dialog.Overlay className="fixed inset-0 z-40 bg-black/50 animate-in fade-in-0 duration-150 md:hidden" />
            <Dialog.Content
              aria-describedby={undefined}
              onOpenAutoFocus={(e) => {
                e.preventDefault();
                (e.currentTarget as HTMLElement | null)?.focus();
              }}
              className="fixed inset-y-0 left-0 z-50 max-w-[85vw] p-2 outline-none animate-in slide-in-from-left-4 duration-200 md:hidden"
            >
              <Dialog.Title className="sr-only">Menu principal</Dialog.Title>
              <Sidebar isMobile onMobileClose={() => setMobileOpen(false)} />
            </Dialog.Content>
          </Dialog.Portal>
        </Dialog.Root>

        <div className="flex min-w-0 flex-1 flex-col overflow-hidden gap-2">
          <Header onMobileMenuOpen={() => setMobileOpen(true)} />
          <main className="flex-1 overflow-auto p-4 md:p-6">
            {/* A chave zera o erro ao navegar: sair da tela quebrada resolve. */}
            <ErrorBoundary key={location.pathname}>
              <Outlet />
            </ErrorBoundary>
          </main>
          {showBottomNav && <BottomNav onMenuOpen={() => setMobileOpen(true)} />}
        </div>
      </div>
    </div>
  );
}
