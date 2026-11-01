import { Link } from 'react-router-dom';
import * as Menu from '@radix-ui/react-dropdown-menu';
import { MoreVertical, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface RowAction {
  label: string;
  icon: LucideIcon;
  onClick?: () => void;
  /** Vira link em vez de botão. */
  to?: string;
  tone?: 'neutral' | 'warning' | 'danger';
  /** Não se aplica a esta linha. A tabela guarda o espaço para o resto não pular. */
  hidden?: boolean;
}

const TONE = {
  neutral: 'hover:bg-brand-50 hover:text-brand-700 dark:hover:bg-white/10 dark:hover:text-brand-300',
  warning: 'hover:bg-amber-50 hover:text-amber-700 dark:hover:bg-amber-900/30 dark:hover:text-amber-300',
  danger: 'hover:bg-red-50 hover:text-red-700 dark:hover:bg-red-900/30 dark:hover:text-red-300',
};

const ICON_BUTTON = 'flex h-8 w-8 items-center justify-center rounded-md text-gray-500 transition-colors dark:text-gray-400';

/** Ações na linha da tabela: ícones em posição fixa, com o nome no leitor de tela. */
export function RowActions({ actions }: { actions: RowAction[] }) {
  return (
    <div className="inline-flex items-center gap-1">
      {actions.map(({ label, icon: Icon, onClick, to, tone = 'neutral', hidden }) => {
        if (hidden) return <span key={label} className="h-8 w-8" aria-hidden />;
        const cls = cn(ICON_BUTTON, TONE[tone]);
        return to ? (
          <Link key={label} to={to} title={label} aria-label={label} className={cls}>
            <Icon size={16} />
          </Link>
        ) : (
          <button key={label} type="button" onClick={onClick} title={label} aria-label={label} className={cls}>
            <Icon size={16} />
          </button>
        );
      })}
    </div>
  );
}

const MENU_TONE = {
  neutral: 'text-gray-800 dark:text-gray-100',
  warning: 'text-amber-800 dark:text-amber-300',
  danger: 'text-red-700 dark:text-red-400',
};

/** Ações no cartão do celular: um menu com alvo de toque de 44px e nomes por extenso. */
export function RowActionsMenu({ actions, subject }: { actions: RowAction[]; subject: string }) {
  const visible = actions.filter((a) => !a.hidden);

  return (
    <Menu.Root>
      <Menu.Trigger
        aria-label={`Ações de ${subject}`}
        className="-mr-1 -mt-1 flex h-11 w-11 shrink-0 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-white/10"
      >
        <MoreVertical size={18} />
      </Menu.Trigger>
      <Menu.Portal>
        <Menu.Content
          align="end"
          sideOffset={4}
          className="z-50 min-w-48 overflow-hidden rounded-lg border border-line bg-raised py-1 shadow-lg animate-in fade-in-0 duration-100"
        >
          {visible.map(({ label, icon: Icon, onClick, to, tone = 'neutral' }) => {
            const cls = cn(
              'flex min-h-11 w-full cursor-pointer items-center gap-3 px-3 text-sm outline-none data-[highlighted]:bg-gray-100 dark:data-[highlighted]:bg-white/10',
              MENU_TONE[tone]
            );
            return to ? (
              <Menu.Item key={label} asChild>
                <Link to={to} className={cls}>
                  <Icon size={16} />
                  {label}
                </Link>
              </Menu.Item>
            ) : (
              <Menu.Item key={label} onSelect={onClick} className={cls}>
                <Icon size={16} />
                {label}
              </Menu.Item>
            );
          })}
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}
