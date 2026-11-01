import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { authService } from '@/services/authService';
import { AuthCard, AuthError, AuthNotice, AuthShell, authLinkClass } from '@/components/base/AuthShell';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';

type TokenState = 'checking' | 'valid' | 'invalid';

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = searchParams.get('token') ?? '';

  const [tokenState, setTokenState] = useState<TokenState>('checking');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    if (!token) {
      setTokenState('invalid');
      return;
    }
    authService
      .validateResetToken(token)
      .then(() => setTokenState('valid'))
      .catch(() => setTokenState('invalid'));
  }, [token]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (password.length < 6) {
      setError('A senha deve ter pelo menos 6 caracteres.');
      return;
    }
    if (password !== confirm) {
      setError('As senhas não coincidem.');
      return;
    }
    setSubmitting(true);
    try {
      await authService.resetPassword(token, password);
      setDone(true);
      setTimeout(() => navigate('/login'), 2500);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      setError(msg ?? 'Não foi possível redefinir a senha. O link pode ter expirado.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthShell subtitle="Definir nova senha">
      <AuthCard>
        {tokenState === 'checking' && (
          <p role="status" className="text-center text-sm text-gray-600 dark:text-gray-300">Validando link...</p>
        )}

        {tokenState === 'invalid' && (
          <div className="space-y-4 text-center">
            <AuthError>Link inválido ou expirado. Solicite um novo.</AuthError>
            <Link to="/forgot-password" className={'block text-sm ' + authLinkClass}>Solicitar novo link</Link>
          </div>
        )}

        {tokenState === 'valid' && done && (
          <div className="space-y-4 text-center">
            <AuthNotice>Senha definida com sucesso! Redirecionando para o login...</AuthNotice>
            <Link to="/login" className={'block text-sm ' + authLinkClass}>Ir para o login</Link>
          </div>
        )}

        {tokenState === 'valid' && !done && (
          <form onSubmit={onSubmit} className="space-y-4">
            <Input
              label="Nova senha"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
              hint="Mínimo de 6 caracteres."
            />
            <Input
              label="Confirmar senha"
              type="password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              autoComplete="new-password"
            />

            {error && <AuthError>{error}</AuthError>}

            <Button type="submit" disabled={submitting} className="w-full">
              {submitting ? 'Salvando...' : 'Definir senha'}
            </Button>
          </form>
        )}
      </AuthCard>
    </AuthShell>
  );
}
