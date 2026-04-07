import { NavLink } from 'react-router-dom';
import { useState } from 'react';
import {
  LayoutDashboard,
  Package,
  Users,
  FileText,
  ShoppingCart,
  Truck,
  Factory,
  Boxes,
  BarChart2,
  Settings,
  ChevronLeft,
  ChevronRight,
  type LucideIcon,
} from 'lucide-react';
import { useAuthStore } from '@/store/authStore';
import { cn } from '@/lib/utils';
import { Logo } from './Logo';

interface NavItem {
  label: string;
  to: string;
  icon: LucideIcon;
  roles?: string[];
}

const navItems: NavItem[] = [
  { label: 'Dashboard',    to: '/dashboard',     icon: LayoutDashboard },
  { label: 'Produtos',     to: '/products',      icon: Package,        roles: ['admin', 'atendente'] },
  { label: 'Clientes',     to: '/customers',     icon: Users,          roles: ['admin', 'atendente'] },
  { label: 'Orçamentos',   to: '/quotations',    icon: FileText,       roles: ['admin', 'atendente'] },
  { label: 'Caixa',        to: '/checkout',      icon: ShoppingCart,   roles: ['admin', 'caixa'] },
  { label: 'Fornecedores', to: '/suppliers',     icon: Truck,          roles: ['admin'] },
  { label: 'Fabricantes',  to: '/manufacturers', icon: Factory,        roles: ['admin'] },
  { label: 'Estoque',      to: '/inventory',     icon: Boxes,          roles: ['admin'] },
  { label: 'Relatórios',   to: '/reports',       icon: BarChart2,      roles: ['admin'] },
  { label: 'Administração',to: '/admin',         icon: Settings,       roles: ['admin'] },
];

export function Sidebar() {
  const { user } = useAuthStore();
  const [collapsed, setCollapsed] = useState(false);

  const visibleItems = navItems.filter(
    (item) => !item.roles || (user && item.roles.includes(user.role))
  );

  return (
    <aside
      className={cn(
        'relative flex flex-col border-r border-gray-200 dark:border-gray-700 bg-white dark:bg-brand-950 transition-all duration-200',
        collapsed ? 'w-15' : 'w-56'
      )}
    >
      {/* Logo */}
      <div className="flex h-14 items-center border-b border-gray-200 dark:border-gray-700 px-3 overflow-hidden">
        {collapsed ? (
          <Logo size={26} className="mx-auto text-brand-600 dark:text-brand-300" />
        ) : (
          <div className="flex items-center gap-2.5 whitespace-nowrap">
            <Logo size={26} className="shrink-0 text-brand-600 dark:text-brand-300" />
            <span className="font-bold text-brand-600 dark:text-brand-300 text-xl tracking-tight">
              Stoctable
            </span>
          </div>
        )}
      </div>

      {/* Nav */}
      <nav className="flex-1 space-y-1 p-2 overflow-hidden">
        {visibleItems.map((item) => {
          const Icon = item.icon;
          return (
            <NavLink
              key={item.to}
              to={item.to}
              title={collapsed ? item.label : undefined}
              className={({ isActive }) =>
                cn(
                  'flex items-center rounded-md px-2 py-2 text-sm font-medium transition-colors',
                  collapsed ? 'justify-center' : 'gap-3',
                  isActive
                    ? 'bg-brand-50 dark:bg-brand-900/30 text-brand-700 dark:text-brand-400'
                    : 'text-gray-600 dark:text-gray-200 hover:bg-brand-50 dark:hover:bg-brand-900/20 hover:text-brand-700 dark:hover:text-white'
                )
              }
            >
              <Icon size={18} className="shrink-0" />
              {!collapsed && <span className="truncate">{item.label}</span>}
            </NavLink>
          );
        })}
      </nav>

      {/* Toggle button */}
      <div className="border-t border-gray-200 dark:border-gray-700 p-2">
        <button
          onClick={() => setCollapsed((c) => !c)}
          title={collapsed ? 'Expandir menu' : 'Recolher menu'}
          className={cn(
            'flex w-full items-center rounded-md px-2 py-2 text-sm text-gray-500 dark:text-gray-400 hover:bg-brand-50 dark:hover:bg-brand-900/20 hover:text-brand-700 dark:hover:text-white transition-colors',
            collapsed ? 'justify-center' : 'gap-3'
          )}
        >
          {collapsed ? <ChevronRight size={18} /> : (
            <>
              <ChevronLeft size={18} className="shrink-0" />
              <span>Recolher</span>
            </>
          )}
        </button>
      </div>
    </aside>
  );
}
