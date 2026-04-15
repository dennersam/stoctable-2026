import { useState, useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useThemeStore } from '@/store/themeStore';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function Layout() {
  const { isDark } = useThemeStore();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  return (
    <div className={`${isDark ? 'dark' : ''} flex h-screen`}>
      <div className="flex h-screen w-full bg-gray-300 dark:bg-zinc-900 p-2 gap-2">

        {/* Sidebar desktop — hidden below md */}
        <div className="hidden md:block">
          <Sidebar />
        </div>

        {/* Sidebar mobile — drawer overlay */}
        {mobileOpen && (
          <div
            className="fixed inset-0 z-40 md:hidden"
            onClick={() => setMobileOpen(false)}
          >
            <div className="absolute inset-0 bg-black/50" />
            <div
              className="absolute inset-y-0 left-0 z-50 p-2"
              onClick={(e) => e.stopPropagation()}
            >
              <Sidebar isMobile onMobileClose={() => setMobileOpen(false)} />
            </div>
          </div>
        )}

        <div className="flex flex-1 flex-col overflow-hidden gap-2">
          <Header onMobileMenuOpen={() => setMobileOpen(true)} />
          <main className="flex-1 overflow-auto p-4 md:p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  );
}
