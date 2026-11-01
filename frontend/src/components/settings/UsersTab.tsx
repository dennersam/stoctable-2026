import { useEffect, useState } from 'react';
import { Plus, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field } from '@/components/ui/Field';
import { fieldClass as inputCls } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
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

  const name = (u: SystemUser) => (
    <>
      <div className="font-medium text-gray-900 dark:text-white">
        {u.fullName}
        {!u.isActive && <Badge variant="neutral" className="ml-2 align-middle">Inativo</Badge>}
      </div>
      <div className="break-all text-xs text-gray-500 dark:text-gray-400">{u.email}</div>
    </>
  );

  const editButton = (u: SystemUser) => (
    <button
      type="button"
      onClick={() => setModal(u)}
      aria-label={'Editar ' + u.fullName}
      title="Editar"
      className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-brand-700 md:h-8 md:w-8 dark:text-gray-400 dark:hover:bg-white/10 dark:hover:text-brand-300"
    >
      <Pencil size={16} />
    </button>
  );

  const columns: Column<SystemUser>[] = [
    { key: 'name', header: 'Nome', cell: name },
    { key: 'username', header: 'Usuário', cell: (u) => u.username },
    { key: 'role', header: 'Perfil', cell: (u) => roleLabel(u.role) },
    { key: 'actions', header: '', align: 'right', cell: editButton },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1 basis-60">
          <h3 className="font-semibold text-gray-900 dark:text-white">Usuários do sistema</h3>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Cadastre usuários e gerencie acessos. A senha é definida pelo próprio usuário via convite por email.
          </p>
        </div>
        <Button size="sm" onClick={() => setModal('new')}><Plus size={16} />Novo usuário</Button>
      </div>

      <DataTable
        caption="Usuários do sistema"
        rows={users}
        columns={columns}
        rowKey={(u) => u.id}
        loading={loading}
        empty="Nenhum usuário cadastrado."
        card={(u) => (
          <div className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              {name(u)}
              <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">{roleLabel(u.role)} · {u.username}</div>
            </div>
            {editButton(u)}
          </div>
        )}
      />

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
    <Modal title={isEdit ? 'Editar usuário' : 'Novo usuário'} onClose={onClose}>
      <ModalBody className="space-y-4">
        <Field label="Nome completo">
          <input className={inputCls} value={fullName} onChange={(e) => setFullName(e.target.value)} />
        </Field>

        <Field label="Nome de usuário" hint={isEdit ? 'O nome de usuário não pode ser alterado.' : undefined}>
          <input
            className={inputCls}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            disabled={isEdit}
            placeholder={isEdit ? undefined : 'login de acesso'}
          />
        </Field>

        <Field label="Email">
          <input type="email" className={inputCls} value={email} onChange={(e) => setEmail(e.target.value)} />
        </Field>

        <Field label="Perfil">
          <select className={inputCls} value={role} onChange={(e) => setRole(e.target.value as UserRole)}>
            {ROLE_OPTIONS.map((r) => (
              <option key={r.value} value={r.value}>{r.label}</option>
            ))}
          </select>
        </Field>

        {isEdit && (
          <label className="flex min-h-11 items-center gap-2 text-sm text-gray-700 dark:text-gray-300 md:min-h-0">
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} className="h-4 w-4 rounded" />
            Usuário ativo
          </label>
        )}

        {!isEdit && (
          <p className="rounded-md bg-gray-50 p-3 text-xs text-gray-600 dark:bg-white/5 dark:text-gray-300">
            O usuário receberá um email com um link para definir a própria senha.
          </p>
        )}
      </ModalBody>
      <ModalFooter>
        <Button variant="outline" onClick={onClose}>Cancelar</Button>
        <Button onClick={handleSave} disabled={saving}>{saving ? 'Salvando...' : 'Salvar'}</Button>
      </ModalFooter>
    </Modal>
  );
}
