import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { isAxiosError } from 'axios';
import { authService } from '@/services/authService';
import { applySession } from '@/lib/session';
import { Logo } from '@/components/base/Logo';

const loginSchema = z.object({
  // O login passou a ser por e-mail: é a identidade única do SaaS inteiro.
  username: z.string().min(1, 'E-mail obrigatório').email('Informe um e-mail válido'),
  password: z.string().min(1, 'Senha obrigatória'),
});

type LoginForm = z.infer<typeof loginSchema>;

export function LoginPage() {
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
  });

  const onSubmit = async (data: LoginForm) => {
    setError(null);
    try {
      const response = await authService.login(data);
      applySession(response);

      // Mais de uma loja: o token recebido é o de pré-filial e não abre nada
      // além da escolha. Uma loja só já vem com a sessão pronta.
      navigate(response.requiresBranchSelection ? '/select-branch' : '/dashboard');
    } catch (err) {
      if (isAxiosError(err) && err.response) {
        const status = err.response.status;
        if (status === 409) {
          // A empresa existe, mas o ambiente ainda está sendo criado — não é
          // erro de credencial, e dizer "senha inválida" aqui seria mentira.
          setError('Estamos preparando o ambiente da sua empresa. Tente novamente em instantes.');
        } else if (status === 401 || status === 400) {
          setError('E-mail ou senha inválidos.');
        } else {
          setError('Erro ao entrar. Tente novamente mais tarde.');
        }
      } else {
        setError('Serviço indisponível no momento. Tente novamente mais tarde.');
      }
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-brand-950">
      <div className="w-full max-w-md px-4">
        <div className="rounded-xl border border-brand-800 bg-brand-900 p-8 shadow-xl">
          <div className="mb-8 text-center">
            <div className="flex items-center justify-center gap-3 mb-1">
              <Logo size={40} className="text-white" />
              <h1 className="text-3xl font-bold text-white tracking-tight">Stoctable</h1>
            </div>
            <p className="mt-2 text-sm text-brand-300">Sistema de Gestão</p>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            <div>
              <label className="block text-sm font-medium text-brand-200 mb-1">E-mail</label>
              <input
                {...register('username')}
                type="email"
                autoComplete="email"
                className="block w-full rounded-md border border-brand-700 bg-brand-800 text-white placeholder-brand-400 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
                placeholder="seu@email.com"
              />
              {errors.username && (
                <p className="mt-1 text-xs text-red-400">{errors.username.message}</p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-brand-200 mb-1">Senha</label>
              <input
                {...register('password')}
                type="password"
                autoComplete="current-password"
                className="block w-full rounded-md border border-brand-700 bg-brand-800 text-white placeholder-brand-400 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
                placeholder="Digite sua senha"
              />
              {errors.password && (
                <p className="mt-1 text-xs text-red-400">{errors.password.message}</p>
              )}
              <div className="mt-1.5 text-right">
                <Link to="/forgot-password" className="text-xs text-brand-300 hover:text-brand-200">
                  Esqueci minha senha
                </Link>
              </div>
            </div>

            {error && (
              <div className="rounded-md bg-red-950 border border-red-800 p-3 text-sm text-red-400">
                {error}
              </div>
            )}

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-brand-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-brand-500 disabled:opacity-60 transition-colors"
            >
              {isSubmitting ? 'Entrando...' : 'Entrar'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
