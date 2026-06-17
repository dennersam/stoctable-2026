import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { authService } from '@/services/authService';
import { Logo } from '@/components/base/Logo';

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
    <div className="flex min-h-screen items-center justify-center bg-brand-950">
      <div className="w-full max-w-md px-4">
        <div className="rounded-xl border border-brand-800 bg-brand-900 p-8 shadow-xl">
          <div className="mb-8 text-center">
            <div className="flex items-center justify-center gap-3 mb-1">
              <Logo size={40} className="text-white" />
              <h1 className="text-3xl font-bold text-white tracking-tight">Stoctable</h1>
            </div>
            <p className="mt-2 text-sm text-brand-300">Definir nova senha</p>
          </div>

          {tokenState === 'checking' && (
            <p className="text-center text-sm text-brand-300">Validando link...</p>
          )}

          {tokenState === 'invalid' && (
            <div className="space-y-5 text-center">
              <div className="rounded-md bg-red-950 border border-red-800 p-4 text-sm text-red-400">
                Link inválido ou expirado. Solicite um novo.
              </div>
              <Link to="/forgot-password" className="block text-sm text-brand-300 hover:text-brand-200">
                Solicitar novo link
              </Link>
            </div>
          )}

          {tokenState === 'valid' && done && (
            <div className="space-y-5 text-center">
              <div className="rounded-md bg-brand-800/40 border border-brand-700 p-4 text-sm text-brand-200">
                Senha definida com sucesso! Redirecionando para o login...
              </div>
              <Link to="/login" className="block text-sm text-brand-300 hover:text-brand-200">
                Ir para o login
              </Link>
            </div>
          )}

          {tokenState === 'valid' && !done && (
            <form onSubmit={onSubmit} className="space-y-5">
              <div>
                <label className="block text-sm font-medium text-brand-200 mb-1">Nova senha</label>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="new-password"
                  className="block w-full rounded-md border border-brand-700 bg-brand-800 text-white placeholder-brand-400 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
                  placeholder="Mínimo 6 caracteres"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-brand-200 mb-1">Confirmar senha</label>
                <input
                  type="password"
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                  autoComplete="new-password"
                  className="block w-full rounded-md border border-brand-700 bg-brand-800 text-white placeholder-brand-400 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
                  placeholder="Repita a senha"
                />
              </div>

              {error && (
                <div className="rounded-md bg-red-950 border border-red-800 p-3 text-sm text-red-400">
                  {error}
                </div>
              )}

              <button
                type="submit"
                disabled={submitting}
                className="w-full rounded-md bg-brand-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-brand-500 disabled:opacity-60 transition-colors"
              >
                {submitting ? 'Salvando...' : 'Definir senha'}
              </button>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
