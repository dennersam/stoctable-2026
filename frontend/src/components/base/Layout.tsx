import { Outlet } from 'react-router-dom';
import { useThemeStore } from '@/store/themeStore';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function Layout() {
  const { isDark } = useThemeStore();

  return (
    <div className={`${isDark ? 'dark' : ''} flex h-screen`}>
      <div className="flex h-screen w-full bg-gray-100 dark:bg-gray-950">
        <Sidebar />
        <div className="flex flex-1 flex-col overflow-hidden">
          <Header />
          <main className="flex-1 overflow-auto p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  );
}
