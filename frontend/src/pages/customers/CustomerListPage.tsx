import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Eye, Pencil, PowerOff, Trash2, X } from 'lucide-react';
import { customerService } from '@/services/customerService';
import type { Customer, CreateCustomerRequest } from '@/types/customer';
import { useAuthStore } from '@/store/authStore';
import { Pagination } from '@/components/base/Pagination';

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

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-950/50 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

function Field({ label, required, children }: {
  label: string; required?: boolean; children: React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {children}
    </div>
  );
}

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="w-full max-w-2xl rounded-xl bg-white dark:bg-brand-900 shadow-xl flex flex-col" style={{ maxHeight: '90vh' }}>

        {/* Header */}
        <div className="flex items-center justify-between border-b border-gray-200 dark:border-brand-800/50 px-5 py-4 shrink-0">
          <h2 className="font-semibold text-gray-900 dark:text-white">
            {isEdit ? 'Editar Cliente' : 'Novo Cliente'}
          </h2>
          <button onClick={onClose} className="rounded-md p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-brand-800/40 hover:text-gray-600 dark:hover:text-white"><X size={16} /></button>
        </div>

        {/* Body */}
        {loading ? (
          <div className="flex-1 flex items-center justify-center py-16 text-gray-500 dark:text-gray-400">Carregando...</div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
            <div className="overflow-y-auto flex-1 p-5 space-y-5">

              {/* Dados pessoais */}
              <section className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Dados pessoais</h3>
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
                  <Field label="Limite de crédito (R$)">
                    <input type="number" min="0" step="0.01" value={form.creditLimit ?? 0}
                      onChange={e => set('creditLimit', parseFloat(e.target.value) || 0)} className={inputCls} />
                  </Field>
                </div>
              </section>

              <div className="border-t border-gray-200 dark:border-brand-800/40" />

              {/* Contato */}
              <section className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Contato</h3>
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
              </section>

              <div className="border-t border-gray-200 dark:border-brand-800/40" />

              {/* Endereço */}
              <section className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Endereço</h3>
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
              </section>

              <div className="border-t border-gray-200 dark:border-brand-800/40" />

              {/* Observações */}
              <section className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Observações</h3>
                <textarea value={form.notes ?? ''} onChange={e => set('notes', e.target.value)}
                  rows={3} placeholder="Informações adicionais sobre o cliente..."
                  className={`${inputCls} resize-none`} />
              </section>
            </div>

            {/* Footer */}
            <div className="flex justify-end gap-3 border-t border-gray-200 dark:border-brand-800/50 px-5 py-4 shrink-0">
              <button type="button" onClick={onClose}
                className="rounded-md border border-gray-300 dark:border-brand-700/50 px-5 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-brand-800/30">
                Cancelar
              </button>
              <button type="submit" disabled={saving}
                className="rounded-md bg-brand-600 hover:bg-brand-700 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">
                {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Cadastrar cliente'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
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
    if (!confirm(`Desativar cliente "${name}"?`)) return;
    try {
      await customerService.deactivate(id);
      toast.success('Cliente desativado.');
      loadCustomers(page, search);
    } catch {
      toast.error('Erro ao desativar cliente.');
    }
  };

  const handleHardDelete = async (id: string, name: string) => {
    if (!confirm(`Excluir permanentemente o cliente "${name}"?\n\nEsta ação não pode ser desfeita.`)) return;
    try {
      await customerService.hardDelete(id);
      toast.success('Cliente excluído.');
      loadCustomers(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este cliente.');
    }
  };

  return (
    <>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Clientes</h1>
          <button
            onClick={() => setModal('new')}
            className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
          >
            Novo cliente
          </button>
        </div>

        <input
          type="text"
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Buscar por nome, CPF/CNPJ, telefone, cidade..."
          className="w-full rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-900/30 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-brand-300/40 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
        />

        {loading ? (
          <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
        ) : (
          <div className="space-y-3">
            <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-brand-800/50">
              <table className="min-w-full divide-y divide-gray-200 dark:divide-brand-800/40">
                <thead className="bg-gray-50 dark:bg-brand-900/40">
                  <tr>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Nome</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">CPF/CNPJ</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Telefone</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">Cidade</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Status</th>
                    {isAdmin && <th className="px-4 py-3" />}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-950">
                  {customers.length === 0 ? (
                    <tr>
                      <td colSpan={isAdmin ? 6 : 5} className="py-8 text-center text-gray-400 dark:text-gray-500">
                        Nenhum cliente encontrado.
                      </td>
                    </tr>
                  ) : customers.map(c => (
                    <tr key={c.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20 transition-colors">
                      <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{c.fullName}</td>
                      <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300 hidden sm:table-cell">
                        {c.documentNumber ? `${c.documentType}: ${c.documentNumber}` : <span className="text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                        {c.mobile || c.phone || <span className="text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300 hidden sm:table-cell">
                        {c.city ? `${c.city}${c.state ? ` / ${c.state}` : ''}` : <span className="text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                          c.isActive
                            ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                            : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                        }`}>
                          {c.isActive ? 'Ativo' : 'Inativo'}
                        </span>
                      </td>
                      {isAdmin && (
                        <td className="px-4 py-3 text-right whitespace-nowrap">
                          <div className="inline-flex items-center gap-1">
                            <Link
                              to={`/customers/${c.id}`}
                              title="Ver detalhes"
                              className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                            >
                              <Eye size={15} />
                            </Link>
                            <button
                              onClick={() => setModal(c.id)}
                              title="Editar"
                              className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                            >
                              <Pencil size={15} />
                            </button>
                            {c.isActive && (
                              <button
                                onClick={() => handleDeactivate(c.id, c.fullName)}
                                title="Desativar"
                                className="rounded p-1.5 text-gray-400 hover:bg-amber-50 hover:text-amber-600 dark:hover:bg-amber-900/30 dark:hover:text-amber-400 transition-colors"
                              >
                                <PowerOff size={15} />
                              </button>
                            )}
                            <button
                              onClick={() => handleHardDelete(c.id, c.fullName)}
                              title="Excluir permanentemente"
                              className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/30 dark:hover:text-red-400 transition-colors"
                            >
                              <Trash2 size={15} />
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <Pagination
              page={page}
              totalPages={totalPages}
              totalCount={totalCount}
              pageSize={PAGE_SIZE}
              onPageChange={setPage}
            />
          </div>
        )}
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
