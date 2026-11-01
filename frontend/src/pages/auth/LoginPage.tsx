import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { isAxiosError } from 'axios';
import { authService } from '@/services/authService';
import { applySession } from '@/lib/session';
import { AuthCard, AuthError, AuthShell, authLinkClass } from '@/components/base/AuthShell';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';

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
    <AuthShell>
      <AuthCard>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <Input
            {...register('username')}
            label="E-mail"
            type="email"
            inputMode="email"
            autoComplete="email"
            autoCapitalize="none"
            placeholder="seu@email.com"
            error={errors.username?.message}
          />

          <div>
            <Input
              {...register('password')}
              label="Senha"
              type="password"
              autoComplete="current-password"
              error={errors.password?.message}
            />
            <div className="mt-2 text-right">
              <Link to="/forgot-password" className={'text-sm ' + authLinkClass}>
                Esqueci minha senha
              </Link>
            </div>
          </div>

          {error && <AuthError>{error}</AuthError>}

          <Button type="submit" disabled={isSubmitting} className="w-full">
            {isSubmitting ? 'Entrando...' : 'Entrar'}
          </Button>
        </form>
      </AuthCard>

      <p className="mt-5 text-center text-sm text-gray-600 dark:text-gray-300">
        Ainda não tem conta?{' '}
        <Link to="/cadastro" className={authLinkClass}>Criar conta</Link>
      </p>
    </AuthShell>
  );
}
