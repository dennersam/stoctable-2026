import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { Eye, Plus, Pencil, PowerOff, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field, FormSection, SearchField } from '@/components/ui/Field';
import { fieldClass as inputCls } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHeader';
import { RowActions, RowActionsMenu, type RowAction } from '@/components/ui/RowActions';
import { Badge } from '@/components/ui/Badge';
import { confirmDialog } from '@/lib/dialogs';
import { customerService } from '@/services/customerService';
import type { Customer, CreateCustomerRequest } from '@/types/customer';
import { useAuthStore } from '@/store/authStore';
import { Pagination } from '@/components/base/Pagination';
import { CurrencyInput } from '@/components/ui/CurrencyInput';

const PAGE_SIZE = 20;

const STATES = [
  'AC','AL','AP','AM','BA','CE','DF','ES','GO','MA','MT','MS',
  'MG','PA','PB','PR','PE','PI','RJ','RN','RS','RO','RR','SC',
  'SP','SE','TO',
];

const EMPTY: CreateCustomerRequest = {
  fullName: '',
  documentType: 'CPF',
  documentNumber: '',
  email: '',
  phone: '',
  mobile: '',
  address: '',
  city: '',
  state: '',
  zipCode: '',
  customerTypeId: undefined,
  creditLimit: 0,
  notes: '',
};

// ─── Customer modal (create / edit) ───────────────────────────────────────────

export function CustomerModal({ customerId, onClose, onSaved }: {
  customerId: string | 'new';
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = customerId !== 'new';
  const [form, setForm] = useState<CreateCustomerRequest>(EMPTY);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isEdit) return;
    customerService.getById(customerId).then((c) => {
      setForm({
        fullName: c.fullName,
        documentType: c.documentType,
        documentNumber: c.documentNumber ?? '',
        email: c.email ?? '',
        phone: c.phone ?? '',
        mobile: c.mobile ?? '',
        address: c.address ?? '',
        city: c.city ?? '',
        state: c.state ?? '',
        zipCode: c.zipCode ?? '',
        customerTypeId: c.customerTypeId,
        creditLimit: c.creditLimit,
        notes: c.notes ?? '',
      });
      setLoading(false);
    }).catch(() => {
      toast.error('Erro ao carregar cliente.');
      onClose();
    });
  }, [customerId, isEdit, onClose]);

  function set<K extends keyof CreateCustomerRequest>(key: K, value: CreateCustomerRequest[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.fullName.trim()) { toast.error('Nome é obrigatório.'); return; }
    setSaving(true);
    try {
      const payload = {
        ...form,
        documentNumber: form.documentNumber?.trim() || undefined,
        email: form.email?.trim() || undefined,
        phone: form.phone?.trim() || undefined,
        mobile: form.mobile?.trim() || undefined,
        address: form.address?.trim() || undefined,
        city: form.city?.trim() || undefined,
        state: form.state?.trim() || undefined,
        zipCode: form.zipCode?.trim() || undefined,
        notes: form.notes?.trim() || undefined,
      };
      if (isEdit) {
        await customerService.update(customerId, payload);
        toast.success('Cliente atualizado.');
      } else {
        await customerService.create(payload);
        toast.success('Cliente cadastrado.');
      }
      onSaved();
      onClose();
    } catch {
      toast.error('Erro ao salvar cliente.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title={isEdit ? 'Editar Cliente' : 'Novo Cliente'} onClose={onClose} size="xl">
        {loading ? (
          <div role="status" className="flex-1 py-16 text-center text-sm text-gray-500 dark:text-gray-400">Carregando...</div>
        ) : (
          <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
            <ModalBody className="space-y-5">

              {/* Dados pessoais */}
              <FormSection title="Dados pessoais">
                <Field label="Nome completo / Razão social" required>
                  <input value={form.fullName} onChange={e => set('fullName', e.target.value)}
                    placeholder="Ex: João da Silva" className={inputCls} />
                </Field>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <Field label="Tipo de documento">
                    <select value={form.documentType} onChange={e => set('documentType', e.target.value as 'CPF' | 'CNPJ')} className={inputCls}>
                      <option value="CPF">CPF</option>
                      <option value="CNPJ">CNPJ</option>
                    </select>
                  </Field>
                  <Field label={form.documentType === 'CPF' ? 'CPF' : 'CNPJ'}>
                    <input value={form.documentNumber ?? ''} onChange={e => set('documentNumber', e.target.value)}
                      placeholder={form.documentType === 'CPF' ? '000.000.000-00' : '00.000.000/0001-00'} className={inputCls} />
                  </Field>
                  <Field label="Limite de crédito">
                    <CurrencyInput value={form.creditLimit ?? 0}
                      onValueChange={v => set('creditLimit', v)} className={inputCls} />
                  </Field>
                </div>
              </FormSection>

              <div className="border-t border-line" />

              {/* Contato */}
              <FormSection title="Contato">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <Field label="E-mail">
                    <input type="email" value={form.email ?? ''} onChange={e => set('email', e.target.value)}
                      placeholder="email@exemplo.com" className={inputCls} />
                  </Field>
                  <Field label="Telefone">
                    <input value={form.phone ?? ''} onChange={e => set('phone', e.target.value)}
                      placeholder="(00) 3000-0000" className={inputCls} />
                  </Field>
                  <Field label="Celular / WhatsApp">
                    <input value={form.mobile ?? ''} onChange={e => set('mobile', e.target.value)}
                      placeholder="(00) 90000-0000" className={inputCls} />
                  </Field>
                </div>
              </FormSection>

              <div className="border-t border-line" />

              {/* Endereço */}
              <FormSection title="Endereço">
                <Field label="Logradouro">
                  <input value={form.address ?? ''} onChange={e => set('address', e.target.value)}
                    placeholder="Rua, número, complemento" className={inputCls} />
                </Field>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <Field label="Cidade">
                    <input value={form.city ?? ''} onChange={e => set('city', e.target.value)}
                      placeholder="Ex: São Paulo" className={inputCls} />
                  </Field>
                  <Field label="Estado">
                    <select value={form.state ?? ''} onChange={e => set('state', e.target.value)} className={inputCls}>
                      <option value="">— UF —</option>
                      {STATES.map(s => <option key={s} value={s}>{s}</option>)}
                    </select>
                  </Field>
                  <Field label="CEP">
                    <input value={form.zipCode ?? ''} onChange={e => set('zipCode', e.target.value)}
                      placeholder="00000-000" className={inputCls} />
                  </Field>
                </div>
              </FormSection>

              <div className="border-t border-line" />

              {/* Observações */}
              <FormSection title="Observações">
                <textarea value={form.notes ?? ''} onChange={e => set('notes', e.target.value)}
                  rows={3} placeholder="Informações adicionais sobre o cliente..."
                  className={inputCls + ' resize-none'} />
              </FormSection>
            </ModalBody>

            <ModalFooter>
              <Button type="button" variant="outline" onClick={onClose}>Cancelar</Button>
              <Button type="submit" disabled={saving}>
                {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Cadastrar cliente'}
              </Button>
            </ModalFooter>
          </form>
        )}
    </Modal>
  );
}

// ─── Customer list page ────────────────────────────────────────────────────────

export function CustomerListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [modal, setModal] = useState<string | 'new' | null>(null);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadCustomers = async (currentPage: number, currentSearch: string) => {
    try {
      setLoading(true);
      const data = await customerService.getAll({
        page: currentPage,
        pageSize: PAGE_SIZE,
        search: currentSearch || undefined,
      });
      setCustomers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar clientes.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadCustomers(page, search);
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadCustomers(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [search]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleDeactivate = async (id: string, name: string) => {
    if (!(await confirmDialog({ title: 'Desativar cliente', description: `"${name}" ficará inativo.`, confirmLabel: 'Desativar cliente' }))) return;
    try {
      await customerService.deactivate(id);
      toast.success('Cliente desativado.');
      loadCustomers(page, search);
    } catch {
      toast.error('Erro ao desativar cliente.');
    }
  };

  const handleHardDelete = async (id: string, name: string) => {
    if (!(await confirmDialog({ title: 'Excluir cliente', description: `"${name}" será excluído permanentemente. Esta ação não pode ser desfeita.`, confirmLabel: 'Excluir cliente', tone: 'danger' }))) return;
    try {
      await customerService.hardDelete(id);
      toast.success('Cliente excluído.');
      loadCustomers(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este cliente.');
    }
  };

  const actionsFor = (c: Customer): RowAction[] => [
    { label: 'Ver detalhes', icon: Eye, to: '/customers/' + c.id },
    { label: 'Editar', icon: Pencil, onClick: () => setModal(c.id) },
    { label: 'Desativar', icon: PowerOff, tone: 'warning', hidden: !c.isActive, onClick: () => handleDeactivate(c.id, c.fullName) },
    { label: 'Excluir permanentemente', icon: Trash2, tone: 'danger', onClick: () => handleHardDelete(c.id, c.fullName) },
  ];

  const name = (c: Customer) => (
    <span className="font-medium text-gray-900 dark:text-white">
      {c.fullName}
      {!c.isActive && <Badge variant="neutral" className="ml-2 align-middle">Inativo</Badge>}
    </span>
  );
  const place = (c: Customer) => (c.city ? c.city + (c.state ? ' / ' + c.state : '') : null);

  const columns: Column<Customer>[] = [
    { key: 'name', header: 'Nome', cell: name },
    { key: 'document', header: 'CPF/CNPJ', className: 'whitespace-nowrap', cell: (c) => (c.documentNumber ? c.documentType + ': ' + c.documentNumber : '—') },
    { key: 'phone', header: 'Telefone', className: 'whitespace-nowrap', cell: (c) => c.mobile || c.phone || '—' },
    { key: 'city', header: 'Cidade', cell: (c) => place(c) ?? '—' },
    ...(isAdmin
      ? [{ key: 'actions', header: '', align: 'right' as const, cell: (c: Customer) => <RowActions actions={actionsFor(c)} /> }]
      : []),
  ];

  return (
    <>
      <div className="space-y-4">
        <PageHeader title="Clientes">
          <Button onClick={() => setModal('new')}><Plus size={16} />Novo cliente</Button>
        </PageHeader>

        <SearchField value={search} onChange={setSearch} placeholder="Buscar por nome, CPF/CNPJ, telefone ou cidade" />

        <DataTable
          caption="Clientes"
          rows={customers}
          columns={columns}
          rowKey={(c) => c.id}
          loading={loading}
          empty={search ? 'Nenhum cliente encontrado para esta busca.' : 'Nenhum cliente cadastrado ainda.'}
          card={(c) => (
            <div className="flex items-start gap-2">
              <div className="min-w-0 flex-1">
                {name(c)}
                <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">{c.mobile || c.phone || 'Sem telefone'}</div>
                <div className="text-xs text-gray-500 dark:text-gray-400">
                  {[c.documentNumber, place(c)].filter(Boolean).join(' · ')}
                </div>
              </div>
              {isAdmin && <RowActionsMenu actions={actionsFor(c)} subject={c.fullName} />}
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
        <CustomerModal
          customerId={modal}
          onClose={() => setModal(null)}
          onSaved={() => loadCustomers(page, search)}
        />
      )}
    </>
  );
}
