import { useEffect, useState } from 'react';
import { Plus, Pencil, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { userService } from '@/services/userService';
import type { SystemUser, CreateUserRequest, UpdateUserRequest } from '@/types/user';
import type { UserRole } from '@/types/common';

const ROLE_OPTIONS: { value: UserRole; label: string }[] = [
  { value: 'admin', label: 'Administrador' },
  { value: 'atendente', label: 'Atendente' },
  { value: 'caixa', label: 'Caixa' },
];

const roleLabel = (role: UserRole) => ROLE_OPTIONS.find((r) => r.value === role)?.label ?? role;

const inputCls =
  'block w-full rounded-md border border-gray-300 dark:border-brand-700 bg-white dark:bg-brand-800 text-gray-900 dark:text-white px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 disabled:opacity-60';

export function UsersTab() {
  const [users, setUsers] = useState<SystemUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState<SystemUser | 'new' | null>(null);

  const loadUsers = async () => {
    setLoading(true);
    try {
      setUsers(await userService.getAll());
    } catch {
      toast.error('Erro ao carregar usuários.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadUsers();
  }, []);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="font-semibold text-gray-900 dark:text-white">Usuários do Sistema</h3>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Cadastre usuários e gerencie acessos. A senha é definida pelo próprio usuário via convite por email.
          </p>
        </div>
        <button
          onClick={() => setModal('new')}
          className="flex shrink-0 items-center gap-1.5 rounded-md bg-brand-600 px-3 py-2 text-sm font-medium text-white hover:bg-brand-500 transition-colors"
        >
          <Plus size={15} /> Novo usuário
        </button>
      </div>

      <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-brand-800/50">
        <table className="min-w-full divide-y divide-gray-200 dark:divide-brand-800/40">
          <thead className="bg-gray-50 dark:bg-brand-900/40">
            <tr>
              <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Nome</th>
              <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Usuário</th>
              <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Perfil</th>
              <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Status</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-900/20">
            {loading ? (
              <tr>
                <td colSpan={5} className="py-8 text-center text-gray-400 dark:text-gray-500">Carregando...</td>
              </tr>
            ) : users.length === 0 ? (
              <tr>
                <td colSpan={5} className="py-8 text-center text-gray-400 dark:text-gray-500">Nenhum usuário cadastrado.</td>
              </tr>
            ) : (
              users.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20">
                  <td className="px-4 py-3">
                    <div className="text-sm font-medium text-gray-900 dark:text-white">{u.fullName}</div>
                    <div className="text-xs text-gray-500 dark:text-gray-400">{u.email}</div>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">{u.username}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">{roleLabel(u.role)}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                        u.isActive
                          ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                          : 'bg-gray-100 text-gray-500 dark:bg-brand-800/40 dark:text-gray-400'
                      }`}
                    >
                      {u.isActive ? 'Ativo' : 'Inativo'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      onClick={() => setModal(u)}
                      title="Editar"
                      className="rounded-md p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-brand-800/40 hover:text-brand-600 dark:hover:text-brand-300 transition-colors"
                    >
                      <Pencil size={15} />
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {modal && (
        <UserModal
          user={modal === 'new' ? null : modal}
          onClose={() => setModal(null)}
          onSaved={() => {
            setModal(null);
            loadUsers();
          }}
        />
      )}
    </div>
  );
}

// ─── Create/Edit modal ────────────────────────────────────────────────────────

interface UserModalProps {
  user: SystemUser | null;
  onClose: () => void;
  onSaved: () => void;
}

function UserModal({ user, onClose, onSaved }: UserModalProps) {
  const isEdit = user !== null;
  const [username, setUsername] = useState(user?.username ?? '');
  const [email, setEmail] = useState(user?.email ?? '');
  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [role, setRole] = useState<UserRole>(user?.role ?? 'atendente');
  const [isActive, setIsActive] = useState(user?.isActive ?? true);
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    if (!fullName.trim() || !email.trim() || (!isEdit && !username.trim())) {
      toast.error('Preencha os campos obrigatórios.');
      return;
    }
    setSaving(true);
    try {
      if (isEdit) {
        const payload: UpdateUserRequest = { fullName: fullName.trim(), email: email.trim(), role, isActive };
        await userService.update(user.id, payload);
        toast.success('Usuário atualizado.');
      } else {
        const payload: CreateUserRequest = {
          username: username.trim(),
          email: email.trim(),
          fullName: fullName.trim(),
          role,
        };
        await userService.create(payload);
        toast.success('Usuário criado. Convite enviado por email.');
      }
      onSaved();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Erro ao salvar usuário.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="w-full max-w-md rounded-2xl bg-white dark:bg-brand-900 shadow-xl overflow-hidden">
        <div className="flex items-center justify-between border-b border-gray-200 dark:border-brand-800/50 px-6 py-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">
            {isEdit ? 'Editar usuário' : 'Novo usuário'}
          </h2>
          <button
            onClick={onClose}
            className="rounded-md p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-brand-800/40 hover:text-gray-600 dark:hover:text-white transition-colors"
          >
            <X size={16} />
          </button>
        </div>

        <div className="space-y-4 p-6">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Nome completo</label>
            <input className={inputCls} value={fullName} onChange={(e) => setFullName(e.target.value)} />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Nome de usuário</label>
            <input
              className={inputCls}
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              disabled={isEdit}
              placeholder={isEdit ? undefined : 'login de acesso'}
            />
            {isEdit && <p className="mt-1 text-xs text-gray-400">O nome de usuário não pode ser alterado.</p>}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Email</label>
            <input type="email" className={inputCls} value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Perfil</label>
            <select className={inputCls} value={role} onChange={(e) => setRole(e.target.value as UserRole)}>
              {ROLE_OPTIONS.map((r) => (
                <option key={r.value} value={r.value}>{r.label}</option>
              ))}
            </select>
          </div>

          {isEdit && (
            <label className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
              <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} className="rounded" />
              Usuário ativo
            </label>
          )}

          {!isEdit && (
            <p className="rounded-md bg-brand-50 dark:bg-brand-800/30 p-3 text-xs text-brand-700 dark:text-brand-300">
              O usuário receberá um email com um link para definir a própria senha.
            </p>
          )}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-200 dark:border-brand-800/50 px-6 py-4">
          <button
            onClick={onClose}
            className="rounded-md px-4 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-brand-800/40 transition-colors"
          >
            Cancelar
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-500 disabled:opacity-60 transition-colors"
          >
            {saving ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </div>
    </div>
  );
}
