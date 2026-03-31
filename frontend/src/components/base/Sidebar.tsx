import { NavLink } from 'react-router-dom';
import { useAuthStore } from '@/store/authStore';
import { cn } from '@/lib/utils';

interface NavItem {
  label: string;
  to: string;
  roles?: string[];
}

const navItems: NavItem[] = [
  { label: 'Dashboard', to: '/dashboard' },
  { label: 'Produtos', to: '/products', roles: ['admin', 'atendente'] },
  { label: 'Clientes', to: '/customers', roles: ['admin', 'atendente'] },
  { label: 'Orçamentos', to: '/quotations', roles: ['admin', 'atendente'] },
  { label: 'Caixa', to: '/checkout', roles: ['admin', 'caixa'] },
  { label: 'Fornecedores', to: '/suppliers', roles: ['admin'] },
  { label: 'Fabricantes', to: '/manufacturers', roles: ['admin'] },
  { label: 'Estoque', to: '/inventory', roles: ['admin'] },
  { label: 'Relatórios', to: '/reports', roles: ['admin'] },
  { label: 'Administração', to: '/admin', roles: ['admin'] },
];

export function Sidebar() {
  const { user } = useAuthStore();

  const visibleItems = navItems.filter(
    (item) => !item.roles || (user && item.roles.includes(user.role))
  );

  return (
    <aside className="flex w-56 flex-col border-r border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
      <div className="flex h-14 items-center px-4 border-b border-gray-200 dark:border-gray-700">
        <span className="font-bold text-blue-600 dark:text-blue-400 text-xl">Stoctable</span>
      </div>
      <nav className="flex-1 space-y-1 p-2">
        {visibleItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              cn(
                'flex items-center rounded-md px-3 py-2 text-sm font-medium transition-colors',
                isActive
                  ? 'bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400'
                  : 'text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-900 dark:hover:text-white'
              )
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
