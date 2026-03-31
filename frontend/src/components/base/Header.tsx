import { useAuthStore } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';
import { useThemeStore } from '@/store/themeStore';
import { useNavigate } from 'react-router-dom';

function SunIcon() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z" />
    </svg>
  );
}

export function Header() {
  const { user, clearAuth } = useAuthStore();
  const { branchName } = useBranchStore();
  const { isDark, toggle } = useThemeStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    clearAuth();
    navigate('/login');
  };

  return (
    <header className="flex h-14 items-center justify-between border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 px-6 shadow-sm">
      <div className="flex items-center gap-2">
        <span className="text-lg font-semibold text-blue-600 dark:text-blue-400">Stoctable</span>
        {branchName && (
          <span className="text-sm text-gray-500 dark:text-gray-400">— {branchName}</span>
        )}
      </div>
      <div className="flex items-center gap-4">
        <span className="text-sm text-gray-700 dark:text-gray-300">
          {user?.fullName}{' '}
          <span className="rounded-full bg-blue-100 dark:bg-blue-900/40 px-2 py-0.5 text-xs font-medium text-blue-700 dark:text-blue-400 capitalize">
            {user?.role}
          </span>
        </span>
        <button
          onClick={toggle}
          title={isDark ? 'Modo claro' : 'Modo escuro'}
          className="rounded-md p-1.5 text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
        >
          {isDark ? <SunIcon /> : <MoonIcon />}
        </button>
        <button
          onClick={handleLogout}
          className="text-sm text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200"
        >
          Sair
        </button>
      </div>
    </header>
  );
}
