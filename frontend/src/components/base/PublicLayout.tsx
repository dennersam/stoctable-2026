import { useEffect, useState } from 'react';
import { Link, Outlet } from 'react-router-dom';
import { Moon, Sun } from 'lucide-react';
import { Logo } from './Logo';
import { Button } from '@/components/ui/Button';
import { useThemeStore } from '@/store/themeStore';
import { useAuthStore } from '@/store/authStore';
import { cn } from '@/lib/utils';

/**
 * Shell das páginas públicas: landing, cadastro e acompanhamento do
 * provisionamento. Não tem sidebar nem exige autenticação — é irmão do Layout
 * da aplicação, não um caso especial dele.
 */
export function PublicLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-white text-gray-900 dark:bg-brand-950 dark:text-white">
      <PublicHeader />
      <main className="flex-1">
        <Outlet />
      </main>
      <PublicFooter />
    </div>
  );
}

function PublicHeader() {
  const { isDark, toggle } = useThemeStore();
  const { isAuthenticated } = useAuthStore();
  const [scrolled, setScrolled] = useState(false);

  // O header é fixo o tempo todo, mas só ganha fundo e borda depois que a
  // página rola — sobre o hero ele fica transparente, e sobre o conteúdo
  // precisa de contraste para o texto não passar por baixo ilegível.
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <header
      className={cn(
        'sticky top-0 z-50 transition-colors',
        scrolled
          ? 'border-b border-gray-200 bg-white/90 backdrop-blur dark:border-white/10 dark:bg-brand-950/90'
          : 'border-b border-transparent'
      )}
    >
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-6">
        <Link
          to="/"
          className="flex items-center gap-2.5 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500"
        >
          <Logo size={28} className="text-brand-600 dark:text-brand-300" />
          <span className="text-lg font-semibold tracking-tight">Stoctable</span>
        </Link>

        <div className="flex items-center gap-2 sm:gap-3">
          <button
            type="button"
            onClick={toggle}
            aria-label={isDark ? 'Usar tema claro' : 'Usar tema escuro'}
            className="rounded-md p-2 text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 dark:text-gray-400 dark:hover:bg-white/10 dark:hover:text-white"
          >
            {isDark ? <Sun size={18} /> : <Moon size={18} />}
          </button>

          {/* Quem já tem sessão não precisa de "Entrar" nem de "Cadastre-se" —
              oferecer cadastro a quem já é cliente confunde. */}
          {isAuthenticated ? (
            <Button asChild size="sm">
              <Link to="/dashboard">Ir para o sistema</Link>
            </Button>
          ) : (
            <>
              <Button asChild variant="ghost" size="sm">
                <Link to="/login">Entrar</Link>
              </Button>

              <Button asChild size="sm">
                <Link to="/cadastro">Cadastre-se</Link>
              </Button>
            </>
          )}
        </div>
      </div>
    </header>
  );
}

function PublicFooter() {
  const year = new Date().getFullYear();

  return (
    <footer className="border-t border-gray-200 dark:border-white/10">
      <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 px-4 py-8 sm:flex-row sm:px-6">
        <div className="flex items-center gap-2.5">
          <Logo size={22} className="text-brand-600 dark:text-brand-300" />
          <span className="text-sm font-medium">Stoctable</span>
        </div>

        <p className="text-sm text-gray-500 dark:text-gray-400">
          © {year} Stoctable. Todos os direitos reservados.
        </p>

        <div className="flex items-center gap-4 text-sm">
          <Link
            to="/login"
            className="text-gray-600 transition-colors hover:text-brand-600 dark:text-gray-400 dark:hover:text-brand-300"
          >
            Entrar
          </Link>
          <Link
            to="/cadastro"
            className="text-gray-600 transition-colors hover:text-brand-600 dark:text-gray-400 dark:hover:text-brand-300"
          >
            Cadastre-se
          </Link>
        </div>
      </div>
    </footer>
  );
}
