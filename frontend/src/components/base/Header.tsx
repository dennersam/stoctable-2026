import { useAuthStore } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';
import { useNavigate } from 'react-router-dom';

export function Header() {
  const { user, clearAuth } = useAuthStore();
  const { branchName } = useBranchStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    clearAuth();
    navigate('/login');
  };

  return (
    <header className="flex h-14 items-center justify-between border-b bg-white px-6 shadow-sm">
      <div className="flex items-center gap-2">
        <span className="text-lg font-semibold text-blue-600">Stoctable</span>
        {branchName && (
          <span className="text-sm text-gray-500">— {branchName}</span>
        )}
      </div>
      <div className="flex items-center gap-4">
        <span className="text-sm text-gray-700">
          {user?.fullName}{' '}
          <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700 capitalize">
            {user?.role}
          </span>
        </span>
        <button
          onClick={handleLogout}
          className="text-sm text-gray-500 hover:text-gray-700"
        >
          Sair
        </button>
      </div>
    </header>
  );
}
