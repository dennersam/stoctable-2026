import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/Button';

/**
 * Antes o catch-all mandava para /login, o que para um visitante que errou a
 * URL era um formulário de login sem explicação — e uma taxa de rejeição alta.
 */
export function NotFoundPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-white px-4 text-center dark:bg-brand-950">
      <p className="text-sm font-semibold uppercase tracking-widest text-brand-600 dark:text-brand-300">
        Erro 404
      </p>
      <h1 className="mt-3 text-3xl font-bold text-gray-900 dark:text-white sm:text-4xl">
        Página não encontrada
      </h1>
      <p className="mt-4 max-w-md text-gray-600 dark:text-gray-300">
        O endereço que você acessou não existe ou foi movido.
      </p>

      <div className="mt-8 flex flex-col gap-3 sm:flex-row">
        <Button asChild>
          <Link to="/">Voltar para o início</Link>
        </Button>
        <Button asChild variant="outline">
          <Link to="/login">Entrar no sistema</Link>
        </Button>
      </div>
    </div>
  );
}
