import { useRef, useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, Settings, User, Users, Camera, KeyRound, Eye, EyeOff, Check, Menu } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuthStore } from '@/store/authStore';
import { useBranchStore } from '@/store/branchStore';
import { BranchSwitcher } from './BranchSwitcher';
import { useThemeStore } from '@/store/themeStore';
import { profileService, resizeImageToBase64, mergeProfileIntoUser } from '@/services/profileService';
import { UsersTab } from '@/components/settings/UsersTab';
import { Modal, ModalBody } from '@/components/ui/Modal';
import { fieldClass as inputCls } from '@/components/ui/fieldClass';

// ─── Helpers ─────────────────────────────────────────────────────────────────

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

function getInitials(fullName: string) {
  const parts = fullName.trim().split(/\s+/);
  if (parts.length === 1) return parts[0][0].toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

// ─── Avatar component (reutilizado em vários lugares) ────────────────────────

function Avatar({ avatarUrl, fullName, size = 'sm' }: { avatarUrl?: string | null; fullName: string; size?: 'sm' | 'lg' }) {
  const initials = fullName ? getInitials(fullName) : '?';
  const cls = size === 'lg'
    ? 'h-20 w-20 text-2xl font-bold'
    : 'h-8 w-8 text-sm font-semibold';

  if (avatarUrl) {
    return (
      <img
        src={avatarUrl}
        alt={fullName}
        className={`${cls} rounded-full object-cover select-none`}
      />
    );
  }

  return (
    <div className={`${cls} flex items-center justify-center rounded-full bg-brand-600 text-white select-none`}>
      {initials}
    </div>
  );
}

// ─── Settings modal — Suas Informações tab ────────────────────────────────────

type SettingsTab = 'profile' | 'users';

interface SettingsModalProps {
  onClose: () => void;
}

function SettingsModal({ onClose }: SettingsModalProps) {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';
  const [activeTab, setActiveTab] = useState<SettingsTab>('profile');

  const tabs: { id: SettingsTab; label: string; icon: React.ReactNode }[] = [
    { id: 'profile', label: 'Suas Informações', icon: <User size={15} /> },
    ...(isAdmin
      ? [{ id: 'users' as SettingsTab, label: 'Usuários do Sistema', icon: <Users size={15} /> }]
      : []),
  ];

  return (
    <Modal title="Configurações" onClose={onClose} size={activeTab === 'users' ? '2xl' : 'xl'}>
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden sm:flex-row">
        {/* Left sidebar / top tabs on mobile */}
        <nav aria-label="Seções das configurações" className="flex w-full shrink-0 flex-row gap-1 border-b border-line p-3 sm:w-48 sm:flex-col sm:border-b-0 sm:border-r">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              aria-current={activeTab === tab.id ? 'page' : undefined}
              className={'flex min-h-11 w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-sm font-medium transition-colors md:min-h-0 ' + (
                activeTab === tab.id
                  ? 'bg-brand-50 text-brand-700 dark:bg-white/10 dark:text-brand-300'
                  : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-white/5 dark:hover:text-white'
              )}
            >
              {tab.icon}
              {tab.label}
            </button>
          ))}
        </nav>

        {/* Right content */}
        <ModalBody className="pb-[max(1.25rem,env(safe-area-inset-bottom))]">
          {activeTab === 'profile' && <ProfileTab />}
          {activeTab === 'users' && isAdmin && <UsersTab />}
        </ModalBody>
      </div>
    </Modal>
  );
}

// ─── Profile tab content ──────────────────────────────────────────────────────

function ProfileTab() {
  const { user, setAuth } = useAuthStore();
  const avatarInputRef = useRef<HTMLInputElement>(null);

  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [savingName, setSavingName] = useState(false);

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);

  const [uploadingAvatar, setUploadingAvatar] = useState(false);

  const refreshToken = typeof window !== 'undefined' ? localStorage.getItem('refreshToken') ?? '' : '';

  const handleSaveName = async () => {
    if (!fullName.trim() || fullName.trim() === user?.fullName) return;
    setSavingName(true);
    try {
      const updated = await profileService.updateMe({ fullName: fullName.trim() });
      const merged = mergeProfileIntoUser(user!, updated);
      setAuth(merged, localStorage.getItem('accessToken')!, refreshToken);
      toast.success('Nome atualizado.');
    } catch {
      toast.error('Erro ao atualizar nome.');
    } finally {
      setSavingName(false);
    }
  };

  const handleSavePassword = async () => {
    if (!currentPassword || !newPassword) return;
    if (newPassword !== confirmPassword) {
      toast.error('As senhas não coincidem.');
      return;
    }
    if (newPassword.length < 6) {
      toast.error('A nova senha deve ter pelo menos 6 caracteres.');
      return;
    }
    setSavingPassword(true);
    try {
      await profileService.updateMe({ currentPassword, newPassword });
      toast.success('Senha alterada com sucesso.');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Senha atual incorreta.');
    } finally {
      setSavingPassword(false);
    }
  };

  const handleAvatarChange = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploadingAvatar(true);
    try {
      const base64 = await resizeImageToBase64(file, 256);
      const updated = await profileService.updateMe({ avatarUrl: base64 });
      const merged = mergeProfileIntoUser(user!, updated);
      setAuth(merged, localStorage.getItem('accessToken')!, refreshToken);
      toast.success('Foto atualizada.');
    } catch {
      toast.error('Erro ao atualizar foto.');
    } finally {
      setUploadingAvatar(false);
      e.target.value = '';
    }
  }, [user, setAuth, refreshToken]);

  const handleRemoveAvatar = async () => {
    setUploadingAvatar(true);
    try {
      const updated = await profileService.updateMe({ avatarUrl: null });
      const merged = mergeProfileIntoUser(user!, updated);
      setAuth(merged, localStorage.getItem('accessToken')!, refreshToken);
      toast.success('Foto removida.');
    } catch {
      toast.error('Erro ao remover foto.');
    } finally {
      setUploadingAvatar(false);
    }
  };

  return (
    <div className="space-y-8">

      {/* ── Foto ──────────────────────────────────────────────────────────── */}
      <section>
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white mb-4">Foto de perfil</h3>
        <div className="flex items-center gap-5">
          <div className="relative shrink-0">
            <Avatar avatarUrl={user?.avatarUrl} fullName={user?.fullName ?? ''} size="lg" />
            <button
              onClick={() => avatarInputRef.current?.click()}
              disabled={uploadingAvatar}
              title="Alterar foto"
              aria-label="Alterar foto"
              className="absolute -bottom-1 -right-1 flex h-7 w-7 items-center justify-center rounded-full bg-brand-600 text-white hover:bg-brand-700 transition-colors disabled:opacity-50"
            >
              <Camera size={13} />
            </button>
            <input
              ref={avatarInputRef}
              type="file"
              accept="image/*"
              className="hidden"
              onChange={handleAvatarChange}
            />
          </div>
          <div className="text-sm text-gray-500 dark:text-gray-400 space-y-1">
            <p>JPG, PNG ou GIF. Máx. 5 MB.</p>
            <p>A imagem será redimensionada para 256×256 px.</p>
            {user?.avatarUrl && (
              <button
                onClick={handleRemoveAvatar}
                disabled={uploadingAvatar}
                className="text-red-500 hover:text-red-600 dark:text-red-400 dark:hover:text-red-300 text-xs transition-colors disabled:opacity-50"
              >
                Remover foto
              </button>
            )}
          </div>
        </div>
      </section>

      <div className="border-t border-line" />

      {/* ── Nome ──────────────────────────────────────────────────────────── */}
      <section>
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white mb-4">Nome de exibição</h3>
        <div className="flex gap-2 max-w-sm">
          <input
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSaveName()}
            placeholder="Seu nome completo"
            className={inputCls}
          />
          <button
            onClick={handleSaveName}
            disabled={savingName || !fullName.trim() || fullName.trim() === user?.fullName}
            title="Salvar nome"
            aria-label="Salvar nome"
            className="flex items-center justify-center rounded-lg bg-brand-600 px-3 text-white hover:bg-brand-700 disabled:opacity-40 transition-colors"
          >
            {savingName ? (
              <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            ) : (
              <Check size={16} />
            )}
          </button>
        </div>
      </section>

      <div className="border-t border-line" />

      {/* ── Senha ─────────────────────────────────────────────────────────── */}
      <section>
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white mb-4 flex items-center gap-2">
          <KeyRound size={14} className="text-brand-500" />
          Alterar senha
        </h3>
        <div className="space-y-3 max-w-sm">
          <div>
            <label htmlFor="current-password" className="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Senha atual</label>
            <div className="relative">
              <input
                id="current-password"
                autoComplete="current-password"
                type={showCurrent ? 'text' : 'password'}
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                placeholder="••••••••"
                className={`${inputCls} pr-10`}
              />
              <button
                type="button"
                onClick={() => setShowCurrent((v) => !v)}
                aria-label={showCurrent ? 'Ocultar senha' : 'Mostrar senha'}
                className="absolute right-1 top-1/2 flex h-9 w-9 -translate-y-1/2 items-center justify-center text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
              >
                {showCurrent ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            </div>
          </div>
          <div>
            <label htmlFor="new-password" className="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Nova senha</label>
            <div className="relative">
              <input
                id="new-password"
                autoComplete="new-password"
                type={showNew ? 'text' : 'password'}
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="••••••••"
                className={`${inputCls} pr-10`}
              />
              <button
                type="button"
                onClick={() => setShowNew((v) => !v)}
                aria-label={showNew ? 'Ocultar senha' : 'Mostrar senha'}
                className="absolute right-1 top-1/2 flex h-9 w-9 -translate-y-1/2 items-center justify-center text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
              >
                {showNew ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            </div>
          </div>
          <div>
            <label htmlFor="confirm-password" className="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Confirmar nova senha</label>
            <input
              id="confirm-password"
              autoComplete="new-password"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSavePassword()}
              placeholder="••••••••"
              className={inputCls}
            />
          </div>
          <button
            onClick={handleSavePassword}
            disabled={savingPassword || !currentPassword || !newPassword || !confirmPassword}
            className="w-full rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-40 transition-colors"
          >
            {savingPassword ? 'Salvando...' : 'Alterar senha'}
          </button>
        </div>
      </section>
    </div>
  );
}

// ─── Header ──────────────────────────────────────────────────────────────────

interface HeaderProps {
  onMobileMenuOpen: () => void;
}

export function Header({ onMobileMenuOpen }: HeaderProps) {
  const { user, clearAuth } = useAuthStore();
  const { branches, activeBranchId } = useBranchStore();
  const activeBranch = branches.find((b) => b.id === activeBranchId) ?? null;
  const { isDark, toggle } = useThemeStore();
  const navigate = useNavigate();

  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    if (dropdownOpen) document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [dropdownOpen]);

  const handleLogout = () => {
    setDropdownOpen(false);
    clearAuth();
    navigate('/login');
  };

  const handleSettings = () => {
    setDropdownOpen(false);
    setSettingsOpen(true);
  };

  return (
    <>
      <header className="flex h-14 shrink-0 items-center justify-between bg-brand-950 px-4 md:px-6 rounded-2xl shadow-md">
        <div className="flex items-center gap-2">
          {/* Hamburger — only on mobile */}
          <button
            onClick={onMobileMenuOpen}
            aria-label="Abrir menu"
            className="md:hidden -ml-2 flex h-11 w-11 items-center justify-center rounded-md text-gray-200 hover:bg-white/10 hover:text-white transition-colors"
          >
            <Menu size={20} />
          </button>

          {/* Seletor de filial. Some sozinho quando a conta só tem uma loja,
              então o caso comum não ganha ruído na barra. */}
          <BranchSwitcher />

          {activeBranch && branches.length === 1 && (
            <span className="hidden sm:block text-sm text-gray-300">{activeBranch.name}</span>
          )}
        </div>

        <div className="flex items-center gap-1 md:gap-3">
          <span className="hidden md:inline-flex items-center gap-2 text-sm text-gray-200">
            {user?.fullName}{' '}
            <span className="rounded-full bg-white/10 px-2 py-0.5 text-xs font-medium text-brand-200 capitalize">
              {user?.role}
            </span>
          </span>

          <button
            onClick={toggle}
            title={isDark ? 'Modo claro' : 'Modo escuro'}
            aria-label={isDark ? 'Usar tema claro' : 'Usar tema escuro'}
            className="flex h-11 w-11 items-center justify-center rounded-md text-gray-200 hover:bg-white/10 hover:text-white transition-colors md:h-9 md:w-9"
          >
            {isDark ? <SunIcon /> : <MoonIcon />}
          </button>

          {/* Avatar + dropdown */}
          <div className="relative" ref={dropdownRef}>
            <button
              onClick={() => setDropdownOpen((o) => !o)}
              title={user?.fullName}
              aria-label="Abrir menu da conta"
              aria-expanded={dropdownOpen}
              className="flex h-11 w-11 items-center justify-center rounded-full hover:ring-2 hover:ring-brand-400 transition-all md:h-9 md:w-9"
            >
              <Avatar avatarUrl={user?.avatarUrl} fullName={user?.fullName ?? ''} size="sm" />
            </button>

            {dropdownOpen && (
              <div className="absolute right-0 top-12 z-40 w-52 rounded-xl bg-raised shadow-lg ring-1 ring-black/10 dark:ring-white/10 py-1 overflow-hidden">
                <button
                  onClick={handleSettings}
                  className="flex min-h-11 w-full items-center gap-3 px-4 py-2.5 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-white/10 transition-colors"
                >
                  <Settings size={15} className="text-gray-500 dark:text-gray-400" />
                  Configurações
                </button>
                <div className="my-1 border-t border-gray-100 dark:border-white/10" />
                <button
                  onClick={handleLogout}
                  className="flex min-h-11 w-full items-center gap-3 px-4 py-2.5 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors"
                >
                  <LogOut size={15} />
                  Sair
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      {settingsOpen && <SettingsModal onClose={() => setSettingsOpen(false)} />}
    </>
  );
}
