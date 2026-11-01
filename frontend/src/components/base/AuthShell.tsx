import { Link } from 'react-router-dom';
import { cn } from '@/lib/utils';
import { Logo } from './Logo';

/** Moldura das telas de autenticação. Segue o tema escolhido, como o resto do sistema. */
export function AuthShell({ subtitle, wide = false, children }: {
  subtitle?: React.ReactNode;
  wide?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center bg-page px-4 py-10">
      <div className={cn('w-full', wide ? 'max-w-lg' : 'max-w-sm')}>
        <div className="mb-6 text-center">
          <Link to="/" className="inline-flex items-center gap-2.5 rounded-md text-gray-900 dark:text-white">
            <Logo size={32} className="text-brand-600 dark:text-brand-300" />
            <span className="font-condensed text-3xl font-bold tracking-tight">Stoctable</span>
          </Link>
          {subtitle && <div className="mt-2 text-sm text-gray-600 dark:text-gray-300">{subtitle}</div>}
        </div>
        {children}
      </div>
    </div>
  );
}

export function AuthCard({ children }: { children: React.ReactNode }) {
  return <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">{children}</div>;
}

export function AuthError({ children }: { children: React.ReactNode }) {
  return (
    <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800 dark:border-red-500/40 dark:bg-red-900/20 dark:text-red-300">
      {children}
    </div>
  );
}

export function AuthNotice({ children }: { children: React.ReactNode }) {
  return (
    <div role="status" className="rounded-md border border-line bg-gray-50 p-3 text-sm text-gray-700 dark:bg-white/5 dark:text-gray-200">
      {children}
    </div>
  );
}

export const authLinkClass = 'inline-block py-2 font-medium text-brand-700 underline-offset-4 hover:underline dark:text-brand-300';
