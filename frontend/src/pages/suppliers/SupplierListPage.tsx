import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { Plus, Pencil, PowerOff, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field, FormSection, SearchField } from '@/components/ui/Field';
import { fieldClass as inputCls } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHeader';
import { RowActions, RowActionsMenu, type RowAction } from '@/components/ui/RowActions';
import { Badge } from '@/components/ui/Badge';
import { confirmDialog } from '@/lib/dialogs';
import { supplierService, type Supplier, type CreateSupplierRequest } from '@/services/supplierService';
import { Pagination } from '@/components/base/Pagination';
import { useAuthStore } from '@/store/authStore';

const PAGE_SIZE = 20;

const EMPTY: CreateSupplierRequest = {
  companyName: '',
  tradeName: '',
  cnpj: '',
  address: '',
  phone: '',
  email: '',
  contactPerson: '',
  notes: '',
};

// ─── Supplier modal (create / edit) ───────────────────────────────────────────

function SupplierModal({ supplierId, onClose, onSaved }: {
  supplierId: string | 'new';
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = supplierId !== 'new';
  const [form, setForm] = useState<CreateSupplierRequest>(EMPTY);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isEdit) return;
    supplierService.getById(supplierId).then(s => {
      setForm({
        companyName: s.companyName,
        tradeName: s.tradeName ?? '',
        cnpj: s.cnpj ?? '',
        address: s.address ?? '',
        phone: s.phone ?? '',
        email: s.email ?? '',
        contactPerson: s.contactPerson ?? '',
        notes: s.notes ?? '',
      });
      setLoading(false);
    }).catch(() => {
      toast.error('Erro ao carregar fornecedor.');
      onClose();
    });
  }, [supplierId, isEdit, onClose]);

  function set<K extends keyof CreateSupplierRequest>(key: K, value: CreateSupplierRequest[K]) {
    setForm(prev => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.companyName.trim()) { toast.error('Razão social é obrigatória.'); return; }
    setSaving(true);
    try {
      const payload: CreateSupplierRequest = {
        companyName: form.companyName.trim(),
        tradeName: form.tradeName?.trim() || undefined,
        cnpj: form.cnpj?.trim() || undefined,
        address: form.address?.trim() || undefined,
        phone: form.phone?.trim() || undefined,
        email: form.email?.trim() || undefined,
        contactPerson: form.contactPerson?.trim() || undefined,
        notes: form.notes?.trim() || undefined,
      };
      if (isEdit) {
        await supplierService.update(supplierId, payload);
        toast.success('Fornecedor atualizado.');
      } else {
        await supplierService.create(payload);
        toast.success('Fornecedor cadastrado.');
      }
      onSaved();
      onClose();
    } catch {
      toast.error('Erro ao salvar fornecedor.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title={isEdit ? 'Editar Fornecedor' : 'Novo Fornecedor'} onClose={onClose} size="xl">
        {loading ? (
          <div role="status" className="flex-1 py-16 text-center text-sm text-gray-500 dark:text-gray-400">Carregando...</div>
        ) : (
          <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
            <ModalBody className="space-y-5">

              {/* Identificação */}
              <FormSection title="Identificação">
                <Field label="Razão social" required>
                  <input value={form.companyName} onChange={e => set('companyName', e.target.value)}
                    placeholder="Ex: Distribuidora Moto Peças Ltda" className={inputCls} />
                </Field>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <Field label="Nome fantasia">
                    <input value={form.tradeName ?? ''} onChange={e => set('tradeName', e.target.value)}
                      placeholder="Ex: MotoDistrib" className={inputCls} />
                  </Field>
                  <Field label="CNPJ">
                    <input value={form.cnpj ?? ''} onChange={e => set('cnpj', e.target.value)}
                      placeholder="00.000.000/0001-00" className={inputCls} />
                  </Field>
                </div>
              </FormSection>

              <div className="border-t border-line" />

              {/* Contato */}
              <FormSection title="Contato">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <Field label="Telefone">
                    <input value={form.phone ?? ''} onChange={e => set('phone', e.target.value)}
                      placeholder="(00) 3000-0000" className={inputCls} />
                  </Field>
                  <Field label="E-mail">
                    <input type="email" value={form.email ?? ''} onChange={e => set('email', e.target.value)}
                      placeholder="contato@fornecedor.com" className={inputCls} />
                  </Field>
                  <Field label="Pessoa de contato">
                    <input value={form.contactPerson ?? ''} onChange={e => set('contactPerson', e.target.value)}
                      placeholder="Nome do responsável" className={inputCls} />
                  </Field>
                </div>
              </FormSection>

              <div className="border-t border-line" />

              {/* Endereço e Obs */}
              <FormSection title="Endereço e observações">
                <Field label="Endereço">
                  <input value={form.address ?? ''} onChange={e => set('address', e.target.value)}
                    placeholder="Rua, número, bairro, cidade / UF" className={inputCls} />
                </Field>
                <Field label="Observações">
                  <textarea value={form.notes ?? ''} onChange={e => set('notes', e.target.value)}
                    rows={3} placeholder="Condições comerciais, prazo de entrega, etc."
                    className={inputCls + ' resize-none'} />
                </Field>
              </FormSection>
            </ModalBody>

            <ModalFooter>
              <Button type="button" variant="outline" onClick={onClose}>Cancelar</Button>
              <Button type="submit" disabled={saving}>
                {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Cadastrar fornecedor'}
              </Button>
            </ModalFooter>
          </form>
        )}
    </Modal>
  );
}

// ─── Supplier list page ────────────────────────────────────────────────────────

export function SupplierListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [modal, setModal] = useState<string | 'new' | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadSuppliers = async (p: number, s: string) => {
    setLoading(true);
    try {
      const data = await supplierService.getAll({ page: p, pageSize: PAGE_SIZE, search: s || undefined });
      setSuppliers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar fornecedores.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSuppliers(page, search);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadSuppliers(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const handleDeactivate = async (s: Supplier) => {
    if (!(await confirmDialog({ title: 'Desativar fornecedor', description: `"${s.companyName}" ficará inativo.`, confirmLabel: 'Desativar fornecedor' }))) return;
    try {
      await supplierService.deactivate(s.id);
      toast.success('Fornecedor desativado.');
      loadSuppliers(page, search);
    } catch {
      toast.error('Erro ao desativar fornecedor.');
    }
  };

  const handleHardDelete = async (s: Supplier) => {
    if (!(await confirmDialog({ title: 'Excluir fornecedor', description: `"${s.companyName}" será excluído permanentemente. Esta ação não pode ser desfeita.`, confirmLabel: 'Excluir fornecedor', tone: 'danger' }))) return;
    try {
      await supplierService.hardDelete(s.id);
      toast.success('Fornecedor excluído.');
      loadSuppliers(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este fornecedor.');
    }
  };

  const actionsFor = (s: Supplier): RowAction[] => [
    { label: 'Editar', icon: Pencil, onClick: () => setModal(s.id) },
    { label: 'Desativar', icon: PowerOff, tone: 'warning', hidden: !s.isActive, onClick: () => handleDeactivate(s) },
    { label: 'Excluir permanentemente', icon: Trash2, tone: 'danger', hidden: !isAdmin, onClick: () => handleHardDelete(s) },
  ];

  const name = (s: Supplier) => (
    <span className="font-medium text-gray-900 dark:text-white">
      {s.companyName}
      {!s.isActive && <Badge variant="neutral" className="ml-2 align-middle">Inativo</Badge>}
    </span>
  );

  const columns: Column<Supplier>[] = [
    { key: 'name', header: 'Razão social', cell: name },
    { key: 'trade', header: 'Nome fantasia', cell: (s) => s.tradeName ?? '—' },
    { key: 'cnpj', header: 'CNPJ', className: 'font-mono text-xs whitespace-nowrap', cell: (s) => s.cnpj ?? '—' },
    { key: 'phone', header: 'Telefone', className: 'whitespace-nowrap', cell: (s) => s.phone ?? '—' },
    { key: 'contact', header: 'Contato', cell: (s) => s.contactPerson ?? '—' },
    { key: 'actions', header: '', align: 'right', cell: (s) => <RowActions actions={actionsFor(s)} /> },
  ];

  return (
    <>
      <div className="space-y-4">
        <PageHeader title="Fornecedores">
          <Button onClick={() => setModal('new')}><Plus size={16} />Novo fornecedor</Button>
        </PageHeader>

        <SearchField value={search} onChange={setSearch} placeholder="Buscar por razão social, fantasia, CNPJ ou telefone" />

        <DataTable
          caption="Fornecedores"
          rows={suppliers}
          columns={columns}
          rowKey={(s) => s.id}
          loading={loading}
          empty={search ? 'Nenhum fornecedor encontrado para esta busca.' : 'Nenhum fornecedor cadastrado ainda.'}
          card={(s) => (
            <div className="flex items-start gap-2">
              <div className="min-w-0 flex-1">
                {name(s)}
                <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">{s.phone ?? 'Sem telefone'}</div>
                <div className="text-xs text-gray-500 dark:text-gray-400">
                  {[s.contactPerson, s.cnpj].filter(Boolean).join(' · ')}
                </div>
              </div>
              <RowActionsMenu actions={actionsFor(s)} subject={s.companyName} />
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
        <SupplierModal
          supplierId={modal}
          onClose={() => setModal(null)}
          onSaved={() => loadSuppliers(page, search)}
        />
      )}
    </>
  );
}
