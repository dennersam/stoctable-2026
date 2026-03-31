import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { isAxiosError } from 'axios';
import { authService } from '@/services/authService';
import { useAuthStore } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';

const loginSchema = z.object({
  username: z.string().min(1, 'Usuário obrigatório'),
  password: z.string().min(1, 'Senha obrigatória'),
});

type LoginForm = z.infer<typeof loginSchema>;

export function LoginPage() {
  const navigate = useNavigate();
  const { setAuth } = useAuthStore();
  const { setBranch } = useBranchStore();
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
      setAuth(response.user, response.accessToken, response.refreshToken);

      // Auto-select first branch if user has only one
      if (response.user.branchIds.length === 1) {
        setBranch(response.user.branchIds[0], '');
      }

      navigate('/dashboard');
    } catch (err) {
      if (isAxiosError(err) && err.response) {
        setError('Usuário ou senha inválidos.');
      } else {
        setError('Serviço indisponível no momento. Tente novamente mais tarde.');
      }
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-950">
      <div className="w-full max-w-md px-4">
        <div className="rounded-xl border border-gray-800 bg-gray-900 p-8 shadow-xl">
          <div className="mb-8 text-center">
            <h1 className="text-3xl font-bold text-blue-500">Stoctable</h1>
            <p className="mt-1 text-sm text-gray-400">Sistema de Gestão</p>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-1">Usuário</label>
              <input
                {...register('username')}
                type="text"
                autoComplete="username"
                className="block w-full rounded-md border border-gray-700 bg-gray-800 text-white placeholder-gray-500 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                placeholder="Digite seu usuário"
              />
              {errors.username && (
                <p className="mt-1 text-xs text-red-400">{errors.username.message}</p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-300 mb-1">Senha</label>
              <input
                {...register('password')}
                type="password"
                autoComplete="current-password"
                className="block w-full rounded-md border border-gray-700 bg-gray-800 text-white placeholder-gray-500 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                placeholder="Digite sua senha"
              />
              {errors.password && (
                <p className="mt-1 text-xs text-red-400">{errors.password.message}</p>
              )}
            </div>

            {error && (
              <div className="rounded-md bg-red-950 border border-red-800 p-3 text-sm text-red-400">
                {error}
              </div>
            )}

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-blue-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-blue-500 disabled:opacity-60 transition-colors"
            >
              {isSubmitting ? 'Entrando...' : 'Entrar'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
