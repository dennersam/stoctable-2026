import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { Plus, Pencil, PowerOff, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field, SearchField } from '@/components/ui/Field';
import { fieldClass as inputCls } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHeader';
import { RowActions, RowActionsMenu, type RowAction } from '@/components/ui/RowActions';
import { Badge } from '@/components/ui/Badge';
import { confirmDialog } from '@/lib/dialogs';
import { manufacturerService } from '@/services/manufacturerService';
import type { Manufacturer } from '@/types/manufacturer';
import { Pagination } from '@/components/base/Pagination';
import { useAuthStore } from '@/store/authStore';

const PAGE_SIZE = 20;

// ─── Modal de cadastro / edição ───────────────────────────────────────────────

interface ModalProps {
  manufacturer?: Manufacturer;
  onClose: () => void;
  onSaved: () => void;
}

function ManufacturerModal({ manufacturer, onClose, onSaved }: ModalProps) {
  const isEdit = Boolean(manufacturer);
  const [name, setName] = useState(manufacturer?.name ?? '');
  const [notes, setNotes] = useState(manufacturer?.notes ?? '');
  const [saving, setSaving] = useState(false);

  async function handleSave() {
    if (!name.trim()) { toast.error('Nome é obrigatório.'); return; }
    setSaving(true);
    try {
      if (isEdit && manufacturer) {
        await manufacturerService.update(manufacturer.id, { name: name.trim(), notes: notes.trim() || undefined });
        toast.success('Fabricante atualizado.');
      } else {
        await manufacturerService.create({ name: name.trim(), notes: notes.trim() || undefined });
        toast.success('Fabricante cadastrado.');
      }
      onSaved();
      onClose();
    } catch {
      toast.error('Erro ao salvar fabricante.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title={isEdit ? 'Editar fabricante' : 'Novo fabricante'} onClose={onClose} size="sm">
      <ModalBody className="space-y-4">
        <Field label="Nome" required>
          <input
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSave()}
            placeholder="Ex: Pirelli, Honda, NGK..."
            className={inputCls}
          />
        </Field>
        <Field label="Observações">
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
            placeholder="Informações adicionais (opcional)"
            className={inputCls + ' resize-none'}
          />
        </Field>
      </ModalBody>
      <ModalFooter>
        <Button variant="outline" onClick={onClose}>Cancelar</Button>
        <Button onClick={handleSave} disabled={saving}>
          {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Cadastrar fabricante'}
        </Button>
      </ModalFooter>
    </Modal>
  );
}

// ─── main page ───────────────────────────────────────────────────────────────

export function ManufacturerListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [manufacturers, setManufacturers] = useState<Manufacturer[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [modal, setModal] = useState<'new' | Manufacturer | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadManufacturers = async (p: number, s: string) => {
    setLoading(true);
    try {
      const data = await manufacturerService.getAll({ page: p, pageSize: PAGE_SIZE, search: s || undefined });
      setManufacturers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar fabricantes.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadManufacturers(page, search);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadManufacturers(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const handleDeactivate = async (m: Manufacturer) => {
    if (!(await confirmDialog({ title: 'Desativar fabricante', description: `"${m.name}" ficará inativo.`, confirmLabel: 'Desativar fabricante' }))) return;
    try {
      await manufacturerService.deactivate(m.id);
      toast.success('Fabricante desativado.');
      loadManufacturers(page, search);
    } catch {
      toast.error('Erro ao desativar fabricante.');
    }
  };

  const handleHardDelete = async (m: Manufacturer) => {
    if (!(await confirmDialog({ title: 'Excluir fabricante', description: `"${m.name}" será excluído permanentemente. Esta ação não pode ser desfeita.`, confirmLabel: 'Excluir fabricante', tone: 'danger' }))) return;
    try {
      await manufacturerService.hardDelete(m.id);
      toast.success('Fabricante excluído.');
      loadManufacturers(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este fabricante.');
    }
  };

  const actionsFor = (m: Manufacturer): RowAction[] => [
    { label: 'Editar', icon: Pencil, onClick: () => setModal(m) },
    { label: 'Desativar', icon: PowerOff, tone: 'warning', hidden: !m.isActive, onClick: () => handleDeactivate(m) },
    { label: 'Excluir permanentemente', icon: Trash2, tone: 'danger', hidden: !isAdmin, onClick: () => handleHardDelete(m) },
  ];

  const name = (m: Manufacturer) => (
    <span className="font-medium text-gray-900 dark:text-white">
      {m.name}
      {!m.isActive && <Badge variant="neutral" className="ml-2 align-middle">Inativo</Badge>}
    </span>
  );

  const columns: Column<Manufacturer>[] = [
    { key: 'name', header: 'Nome', cell: name },
    { key: 'notes', header: 'Observações', className: 'max-w-md truncate', cell: (m) => m.notes || '—' },
    { key: 'actions', header: '', align: 'right', cell: (m) => <RowActions actions={actionsFor(m)} /> },
  ];

  return (
    <>
      <div className="space-y-4">
        <PageHeader title="Fabricantes">
          <Button onClick={() => setModal('new')}><Plus size={16} />Novo fabricante</Button>
        </PageHeader>

        <SearchField value={search} onChange={setSearch} placeholder="Buscar por nome" />

        <DataTable
          caption="Fabricantes"
          rows={manufacturers}
          columns={columns}
          rowKey={(m) => m.id}
          loading={loading}
          empty={search ? 'Nenhum fabricante encontrado para esta busca.' : 'Nenhum fabricante cadastrado ainda.'}
          card={(m) => (
            <div className="flex items-start gap-2">
              <div className="min-w-0 flex-1">
                {name(m)}
                {m.notes && <div className="mt-1 text-sm text-gray-500 dark:text-gray-400">{m.notes}</div>}
              </div>
              <RowActionsMenu actions={actionsFor(m)} subject={m.name} />
            </div>
          )}
        />

        <Pagination
          page={page}
          totalPages={totalPages}
          totalCount={totalCount}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
        />
      </div>

      {modal !== null && (
        <ManufacturerModal
          manufacturer={modal === 'new' ? undefined : modal}
          onClose={() => setModal(null)}
          onSaved={() => loadManufacturers(page, search)}
        />
      )}
    </>
  );
}
