import { NavLink } from 'react-router-dom';
import {
  Boxes,
  FileText,
  LayoutDashboard,
  Menu,
  Package,
  ShoppingCart,
  Users,
  ArrowLeftRight,
  type LucideIcon,
} from 'lucide-react';
import { useAuthStore } from '@/store/authStore';
import { cn } from '@/lib/utils';

interface Tab {
  label: string;
  to: string;
  icon: LucideIcon;
}

const HOME: Tab = { label: 'Início', to: '/dashboard', icon: LayoutDashboard };
const QUOTATIONS: Tab = { label: 'Orçamentos', to: '/quotations', icon: FileText };
const CHECKOUT: Tab = { label: 'Caixa', to: '/checkout', icon: ShoppingCart };

// O que cada perfil abre várias vezes ao dia. O resto continua no menu.
const TABS_BY_ROLE: Record<string, Tab[]> = {
  admin: [HOME, QUOTATIONS, CHECKOUT, { label: 'Estoque', to: '/inventory', icon: Boxes }],
  atendente: [
    HOME,
    QUOTATIONS,
    { label: 'Produtos', to: '/products', icon: Package },
    { label: 'Clientes', to: '/customers', icon: Users },
  ],
  caixa: [HOME, CHECKOUT, { label: 'Transferências', to: '/transfers', icon: ArrowLeftRight }],
};

const TAB = 'flex min-h-14 flex-1 flex-col items-center justify-center gap-0.5 text-[11px] font-medium';

export function BottomNav({ onMenuOpen }: { onMenuOpen: () => void }) {
  const role = useAuthStore((s) => s.user?.role);
  const tabs = (role && TABS_BY_ROLE[role]) || [HOME];

  return (
    <nav
      aria-label="Atalhos"
      className="flex shrink-0 rounded-2xl bg-brand-950 pb-[env(safe-area-inset-bottom)] shadow-md md:hidden"
    >
      {tabs.map(({ label, to, icon: Icon }) => (
        <NavLink
          key={to}
          to={to}
          className={({ isActive }) => cn(TAB, 'min-w-0 truncate', isActive ? 'text-brand-300' : 'text-gray-300')}
        >
          <Icon size={20} />
          {label}
        </NavLink>
      ))}
      <button type="button" onClick={onMenuOpen} className={cn(TAB, 'text-gray-300')}>
        <Menu size={20} />
        Menu
      </button>
    </nav>
  );
}
