import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { Pencil, PowerOff, Trash2, X } from 'lucide-react';
import { supplierService, type Supplier, type CreateSupplierRequest } from '@/services/supplierService';
import { Pagination } from '@/components/base/Pagination';
import { useAuthStore } from '@/store/authStore';

const PAGE_SIZE = 20;

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-950/50 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="w-full max-w-2xl rounded-xl bg-white dark:bg-brand-900 shadow-xl flex flex-col" style={{ maxHeight: '90vh' }}>

        {/* Header */}
        <div className="flex items-center justify-between border-b border-gray-200 dark:border-brand-800/50 px-5 py-4 shrink-0">
          <h2 className="font-semibold text-gray-900 dark:text-white">
            {isEdit ? 'Editar Fornecedor' : 'Novo Fornecedor'}
          </h2>
          <button onClick={onClose} className="rounded-md p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-brand-800/40 hover:text-gray-600 dark:hover:text-white"><X size={16} /></button>
        </div>

        {/* Body */}
        {loading ? (
          <div className="flex-1 flex items-center justify-center py-16 text-gray-500 dark:text-gray-400">Carregando...</div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
            <div className="overflow-y-auto flex-1 p-5 space-y-5">

              {/* Identificação */}
              <section className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Identificação</h3>
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
              </section>

              <div className="border-t border-gray-200 dark:border-brand-800/40" />

              {/* Contato */}
              <section className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Contato</h3>
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
              </section>

              <div className="border-t border-gray-200 dark:border-brand-800/40" />

              {/* Endereço e Obs */}
              <section className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Endereço e observações</h3>
                <Field label="Endereço">
                  <input value={form.address ?? ''} onChange={e => set('address', e.target.value)}
                    placeholder="Rua, número, bairro, cidade / UF" className={inputCls} />
                </Field>
                <Field label="Observações">
                  <textarea value={form.notes ?? ''} onChange={e => set('notes', e.target.value)}
                    rows={3} placeholder="Condições comerciais, prazo de entrega, etc."
                    className={`${inputCls} resize-none`} />
                </Field>
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
                {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Cadastrar fornecedor'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
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
    if (!confirm(`Desativar fornecedor "${s.companyName}"?`)) return;
    try {
      await supplierService.deactivate(s.id);
      toast.success('Fornecedor desativado.');
      loadSuppliers(page, search);
    } catch {
      toast.error('Erro ao desativar fornecedor.');
    }
  };

  const handleHardDelete = async (s: Supplier) => {
    if (!confirm(`Excluir permanentemente o fornecedor "${s.companyName}"?\n\nEsta ação não pode ser desfeita.`)) return;
    try {
      await supplierService.hardDelete(s.id);
      toast.success('Fornecedor excluído.');
      loadSuppliers(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este fornecedor.');
    }
  };

  return (
    <>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Fornecedores</h1>
          <button
            onClick={() => setModal('new')}
            className="rounded-md bg-brand-600 hover:bg-brand-700 px-4 py-2 text-sm font-medium text-white"
          >
            Novo fornecedor
          </button>
        </div>

        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Buscar por razão social, fantasia, CNPJ ou telefone..."
          className="w-full rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-900/30 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-brand-300/40 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
        />

        {loading ? (
          <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
        ) : (
          <>
            <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-brand-800/50">
              <table className="min-w-full divide-y divide-gray-200 dark:divide-brand-800/40">
                <thead className="bg-gray-50 dark:bg-brand-900/40">
                  <tr>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Razão social</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">Nome fantasia</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">CNPJ</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Telefone</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">Contato</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Status</th>
                    <th className="px-4 py-3" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-950">
                  {suppliers.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="py-8 text-center text-gray-400 dark:text-gray-500">
                        Nenhum fornecedor encontrado.
                      </td>
                    </tr>
                  ) : suppliers.map(s => (
                    <tr key={s.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20 transition-colors">
                      <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{s.companyName}</td>
                      <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300 hidden sm:table-cell">
                        {s.tradeName ?? <span className="text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3 text-sm font-mono text-gray-600 dark:text-gray-300 hidden sm:table-cell">
                        {s.cnpj ?? <span className="text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
                        {s.phone ?? <span className="text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-300 hidden sm:table-cell">
                        {s.contactPerson ?? <span className="text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                          s.isActive
                            ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                            : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                        }`}>
                          {s.isActive ? 'Ativo' : 'Inativo'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        <div className="inline-flex items-center gap-1">
                          <button
                            onClick={() => setModal(s.id)}
                            title="Editar"
                            className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                          >
                            <Pencil size={15} />
                          </button>
                          {s.isActive && (
                            <button
                              onClick={() => handleDeactivate(s)}
                              title="Desativar"
                              className="rounded p-1.5 text-gray-400 hover:bg-amber-50 hover:text-amber-600 dark:hover:bg-amber-900/30 dark:hover:text-amber-400 transition-colors"
                            >
                              <PowerOff size={15} />
                            </button>
                          )}
                          {isAdmin && (
                            <button
                              onClick={() => handleHardDelete(s)}
                              title="Excluir permanentemente"
                              className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/30 dark:hover:text-red-400 transition-colors"
                            >
                              <Trash2 size={15} />
                            </button>
                          )}
                        </div>
                      </td>
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
          </>
        )}
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
