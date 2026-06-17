import { useState } from 'react';
import { Link } from 'react-router-dom';
import { authService } from '@/services/authService';
import { Logo } from '@/components/base/Logo';

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;
    setSubmitting(true);
    try {
      await authService.forgotPassword(email.trim());
    } catch {
      // Resposta neutra mesmo em erro — não revelamos se o email existe.
    } finally {
      setSubmitting(false);
      setSent(true);
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
            <p className="mt-2 text-sm text-brand-300">Recuperação de senha</p>
          </div>

          {sent ? (
            <div className="space-y-5 text-center">
              <div className="rounded-md bg-brand-800/40 border border-brand-700 p-4 text-sm text-brand-200">
                Se o email estiver cadastrado, enviamos um link para redefinir sua senha. Verifique sua caixa de entrada.
              </div>
              <Link to="/login" className="block text-sm text-brand-300 hover:text-brand-200">
                Voltar ao login
              </Link>
            </div>
          ) : (
            <form onSubmit={onSubmit} className="space-y-5">
              <div>
                <label className="block text-sm font-medium text-brand-200 mb-1">Email</label>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  autoComplete="email"
                  className="block w-full rounded-md border border-brand-700 bg-brand-800 text-white placeholder-brand-400 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
                  placeholder="Digite seu email"
                />
              </div>

              <button
                type="submit"
                disabled={submitting}
                className="w-full rounded-md bg-brand-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-brand-500 disabled:opacity-60 transition-colors"
              >
                {submitting ? 'Enviando...' : 'Enviar link de redefinição'}
              </button>

              <Link to="/login" className="block text-center text-sm text-brand-300 hover:text-brand-200">
                Voltar ao login
              </Link>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
