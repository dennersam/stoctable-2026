import { NavLink } from 'react-router-dom';
import { useState } from 'react';
import {
  ArrowLeftRight,
  Network,
  LayoutDashboard,
  Package,
  Users,
  FileText,
  ShoppingCart,
  Truck,
  Factory,
  Boxes,
  ChevronLeft,
  ChevronRight,
  X,
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

const navGroups: { label?: string; items: NavItem[] }[] = [
  {
    items: [
      { label: 'Dashboard', to: '/dashboard', icon: LayoutDashboard },
    ],
  },
  {
    label: 'Vendas',
    items: [
      { label: 'Orçamentos', to: '/quotations', icon: FileText, roles: ['admin', 'atendente'] },
      { label: 'Caixa', to: '/checkout', icon: ShoppingCart, roles: ['admin', 'caixa'] },
      { label: 'Clientes', to: '/customers', icon: Users, roles: ['admin', 'atendente'] },
    ],
  },
  {
    label: 'Estoque',
    items: [
      { label: 'Produtos', to: '/products', icon: Package, roles: ['admin', 'atendente'] },
      { label: 'Estoque', to: '/inventory', icon: Boxes, roles: ['admin'] },
      { label: 'Transferências', to: '/transfers', icon: ArrowLeftRight },
      { label: 'Estoque na rede', to: '/network-stock', icon: Network },
    ],
  },
  {
    label: 'Cadastros',
    items: [
      { label: 'Fornecedores', to: '/suppliers', icon: Truck, roles: ['admin'] },
      { label: 'Fabricantes', to: '/manufacturers', icon: Factory, roles: ['admin'] },
    ],
  },
];

const COLLAPSED_KEY = 'stoctable-sidebar-collapsed';

function readCollapsed() {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === 'true';
  } catch {
    return false;
  }
}

interface SidebarProps {
  isMobile?: boolean;
  onMobileClose?: () => void;
}

export function Sidebar({ isMobile = false, onMobileClose }: SidebarProps) {
  const { user } = useAuthStore();
  const [collapsed, setCollapsed] = useState(readCollapsed);

  const toggleCollapsed = () => {
    setCollapsed((c) => {
      try {
        localStorage.setItem(COLLAPSED_KEY, String(!c));
      } catch {
        // Modo privativo: o menu só não lembra a escolha no próximo acesso.
      }
      return !c;
    });
  };

  const isCollapsed = !isMobile && collapsed;

  const visibleGroups = navGroups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => !item.roles || (user && item.roles.includes(user.role))),
    }))
    .filter((group) => group.items.length > 0);

  return (
    <aside
      className={cn(
        'relative flex h-full flex-col overflow-hidden bg-brand-950 rounded-2xl shadow-md transition-all duration-200',
        isMobile
          ? 'w-64'
          : collapsed ? 'w-15' : 'w-56'
      )}
    >
      {/* Logo */}
      <div className="flex h-14 shrink-0 items-center border-b border-white/10 px-3 overflow-hidden">
        {isCollapsed ? (
          <Logo size={26} className="mx-auto text-brand-300" />
        ) : (
          <div className="flex w-full items-center justify-center gap-2.5 whitespace-nowrap">
            <Logo size={26} className="shrink-0 text-brand-300" />
            <span className="font-bold text-brand-300 text-xl tracking-tight">
              Stoctable
            </span>
          </div>
        )}
        {isMobile && (
          <button
            onClick={onMobileClose}
            aria-label="Fechar menu"
            className="absolute right-1.5 top-1.5 flex h-11 w-11 items-center justify-center rounded-md text-gray-300 hover:bg-white/10 hover:text-white"
          >
            <X size={18} />
          </button>
        )}
      </div>

      {/* Nav */}
      <nav aria-label="Principal" className="flex-1 space-y-4 overflow-y-auto p-2">
        {visibleGroups.map((group, i) => (
          <div key={group.label ?? i} className="space-y-1">
            {group.label && !isCollapsed && (
              <p className="px-2 pb-0.5 text-xs font-medium text-gray-400">{group.label}</p>
            )}
            {group.items.map((item) => {
              const Icon = item.icon;
              return (
                <NavLink
                  key={item.to}
                  to={item.to}
                  title={isCollapsed ? item.label : undefined}
                  aria-label={isCollapsed ? item.label : undefined}
                  onClick={isMobile ? onMobileClose : undefined}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center rounded-md px-2 py-2 text-sm font-medium transition-colors focus-visible:outline-brand-300',
                      (isMobile || !isCollapsed) && 'pointer-coarse:min-h-11',
                      isMobile && 'min-h-11',
                      isCollapsed ? 'justify-center' : 'gap-3',
                      isActive
                        ? 'bg-white/10 text-brand-300'
                        : 'text-gray-300 hover:bg-white/5 hover:text-white'
                    )
                  }
                >
                  <Icon size={18} className="shrink-0" />
                  {!isCollapsed && <span className="truncate">{item.label}</span>}
                </NavLink>
              );
            })}
          </div>
        ))}
      </nav>

      {/* Toggle button — hidden on mobile drawer */}
      {!isMobile && (
        <div className="shrink-0 border-t border-white/10 p-2">
          <button
            onClick={toggleCollapsed}
            aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}
            title={collapsed ? 'Expandir menu' : 'Recolher menu'}
            className={cn(
              'flex w-full items-center rounded-md px-2 py-2 text-sm text-gray-300 hover:bg-white/5 hover:text-white transition-colors focus-visible:outline-brand-300',
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
      )}
    </aside>
  );
}
