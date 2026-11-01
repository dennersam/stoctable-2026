import { useState } from 'react';
import { Link } from 'react-router-dom';
import { authService } from '@/services/authService';
import { AuthCard, AuthNotice, AuthShell, authLinkClass } from '@/components/base/AuthShell';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';

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
    <AuthShell subtitle="Recuperação de senha">
      <AuthCard>
        {sent ? (
          <AuthNotice>
            Se o email estiver cadastrado, enviamos um link para redefinir sua senha. Verifique sua caixa de entrada.
          </AuthNotice>
        ) : (
          <form onSubmit={onSubmit} className="space-y-4">
            <Input
              label="E-mail"
              type="email"
              inputMode="email"
              autoCapitalize="none"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
              placeholder="seu@email.com"
            />
            <Button type="submit" disabled={submitting} className="w-full">
              {submitting ? 'Enviando...' : 'Enviar link de redefinição'}
            </Button>
          </form>
        )}
      </AuthCard>

      <p className="mt-5 text-center text-sm">
        <Link to="/login" className={authLinkClass}>Voltar ao login</Link>
      </p>
    </AuthShell>
  );
}
