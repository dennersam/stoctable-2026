import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Building2, Mail } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Card, CardTitle, CardDescription } from '@/components/ui/Card';

/**
 * Espaço reservado do cadastro de empresa.
 *
 * O assistente de verdade — consulta de CNPJ, filiais, usuário administrador e
 * a tela de "preparando seu ambiente" — depende do provisionamento automático,
 * que é a próxima fase. Até lá esta página existe para que o botão principal da
 * landing não caia num 404, e para dar um caminho real a quem chegar aqui.
 */
export function SignupPage() {
  useEffect(() => {
    const anterior = document.title;
    document.title = 'Cadastre sua empresa — Stoctable';
    return () => {
      document.title = anterior;
    };
  }, []);

  return (
    <div className="mx-auto max-w-2xl px-4 py-20 sm:px-6 lg:py-28">
      <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-brand-100 text-brand-700 dark:bg-brand-900 dark:text-brand-200">
        <Building2 size={24} />
      </div>

      <h1 className="mt-6 text-3xl font-bold tracking-tight sm:text-4xl">
        Cadastro de empresa
      </h1>
      <p className="mt-4 text-lg leading-relaxed text-gray-600 dark:text-gray-300">
        O cadastro por conta própria está sendo finalizado. Enquanto isso, nós
        criamos o ambiente da sua empresa para você — com as filiais já
        configuradas e os cadastros do seu sistema atual importados.
      </p>

      <Card className="mt-10">
        <CardTitle>Como pedir seu acesso</CardTitle>
        <CardDescription>
          Envie o CNPJ da matriz e das filiais, e o nome de quem vai administrar
          a conta. Respondemos com o acesso pronto.
        </CardDescription>

        <div className="mt-6">
          <Button asChild>
            <a href="mailto:contato@stoctable.com.br?subject=Quero%20cadastrar%20minha%20empresa">
              <Mail size={18} />
              Falar com a gente
            </a>
          </Button>
        </div>
      </Card>

      <p className="mt-10 text-sm text-gray-500 dark:text-gray-400">
        Já tem conta?{' '}
        <Link
          to="/login"
          className="font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-300"
        >
          Entrar no sistema
        </Link>
      </p>
    </div>
  );
}
