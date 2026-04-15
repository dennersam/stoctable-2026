import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { Pencil, PowerOff, Trash2, X } from 'lucide-react';
import { productService } from '@/services/productService';
import { manufacturerService } from '@/services/manufacturerService';
import { supplierService, type SupplierOption } from '@/services/supplierService';
import type { Product, CreateProductRequest } from '@/types/product';
import type { Manufacturer } from '@/types/manufacturer';
import { useAuthStore } from '@/store/authStore';
import { Pagination } from '@/components/base/Pagination';

const PAGE_SIZE = 20;

const UNITS = ['un', 'pc', 'par', 'kit', 'cx', 'kg', 'g', 'l', 'ml', 'm', 'cm'];

const EMPTY: CreateProductRequest = {
  barcode: '',
  name: '',
  manufacturerId: undefined,
  categoryId: undefined,
  supplierId: undefined,
  costPrice: 0,
  salePrice: 0,
  stockMinimum: 0,
  unit: 'un',
  icmsRate: undefined,
  ipiRate: undefined,
  cst: '',
  ncm: '',
  notes: '',
};

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-950/50 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

function Field({ label, required, hint, children }: {
  label: string; required?: boolean; hint?: string; children: React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {children}
      {hint && <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">{hint}</p>}
    </div>
  );
}

// ─── Quick-add manufacturer modal ─────────────────────────────────────────────

function NewManufacturerModal({ onClose, onCreate }: {
  onClose: () => void;
  onCreate: (m: Manufacturer) => void;
}) {
  const [name, setName] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);

  async function handleSave() {
    if (!name.trim()) { toast.error('Informe o nome do fabricante.'); return; }
    setSaving(true);
    try {
      const created = await manufacturerService.create({ name: name.trim(), notes: notes.trim() || undefined });
      toast.success(`Fabricante "${created.name}" cadastrado.`);
      onCreate(created);
    } catch {
      toast.error('Erro ao cadastrar fabricante.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="w-full max-w-sm rounded-xl bg-white dark:bg-brand-900 shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 dark:border-brand-800/50 px-5 py-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">Novo Fabricante</h2>
          <button onClick={onClose} className="rounded-md p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-brand-800/40 hover:text-gray-600 dark:hover:text-white"><X size={16} /></button>
        </div>
        <div className="p-5 space-y-4">
          <Field label="Nome" required>
            <input autoFocus value={name} onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSave()}
              placeholder="Ex: Pirelli, Honda, NGK..." className={inputCls} />
          </Field>
          <Field label="Observações">
            <textarea value={notes} onChange={(e) => setNotes(e.target.value)}
              rows={2} placeholder="Informações adicionais (opcional)"
              className={`${inputCls} resize-none`} />
          </Field>
          <div className="flex gap-2 justify-end pt-1">
            <button onClick={onClose} className="rounded-md border border-gray-300 dark:border-brand-700/50 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-brand-800/30">Cancelar</button>
            <button onClick={handleSave} disabled={saving} className="rounded-md bg-brand-600 hover:bg-brand-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
              {saving ? 'Salvando...' : 'Cadastrar'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Product modal (create / edit) ────────────────────────────────────────────

function ProductModal({ productId, onClose, onSaved }: {
  productId: string | 'new';
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = productId !== 'new';
  const [form, setForm] = useState<CreateProductRequest>(EMPTY);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [manufacturers, setManufacturers] = useState<Manufacturer[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierOption[]>([]);
  const [showNewManufacturer, setShowNewManufacturer] = useState(false);

  useEffect(() => {
    manufacturerService.getActive().then(setManufacturers).catch(() => {});
    supplierService.getAllForSelect().then(setSuppliers).catch(() => {});
  }, []);

  useEffect(() => {
    if (!isEdit) return;
    productService.getById(productId).then((p) => {
      setForm({
        barcode: p.barcode ?? '',
        name: p.name,
        manufacturerId: p.manufacturerId,
        categoryId: p.categoryId,
        supplierId: p.supplierId,
        costPrice: p.costPrice,
        salePrice: p.salePrice,
        stockMinimum: p.stockMinimum,
        unit: p.unit,
        icmsRate: p.icmsRate,
        ipiRate: p.ipiRate,
        cst: p.cst ?? '',
        ncm: p.ncm ?? '',
        notes: p.notes ?? '',
      });
      setLoading(false);
    }).catch(() => {
      toast.error('Erro ao carregar produto.');
      onClose();
    });
  }, [productId, isEdit, onClose]);

  function set<K extends keyof CreateProductRequest>(key: K, value: CreateProductRequest[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function numericInput(key: keyof CreateProductRequest) {
    return (e: React.ChangeEvent<HTMLInputElement>) => {
      const v = e.target.value === '' ? undefined : parseFloat(e.target.value);
      setForm((prev) => ({ ...prev, [key]: v }));
    };
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim()) { toast.error('Nome do produto é obrigatório.'); return; }
    setSaving(true);
    try {
      if (isEdit) {
        await productService.update(productId, form);
        toast.success('Produto atualizado.');
      } else {
        await productService.create(form);
        toast.success('Produto criado.');
      }
      onSaved();
      onClose();
    } catch {
      toast.error('Erro ao salvar produto.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
        <div className="w-full max-w-2xl rounded-xl bg-white dark:bg-brand-900 shadow-xl flex flex-col" style={{ maxHeight: '90vh' }}>

          {/* Header */}
          <div className="flex items-center justify-between border-b border-gray-200 dark:border-brand-800/50 px-5 py-4 shrink-0">
            <h2 className="font-semibold text-gray-900 dark:text-white">
              {isEdit ? 'Editar Produto' : 'Novo Produto'}
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
                  <Field label="Nome do produto" required>
                    <input value={form.name} onChange={(e) => set('name', e.target.value)}
                      placeholder="Ex: Pneu Traseiro 90/90-18 Pirelli" className={inputCls} />
                  </Field>
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <Field label="Fabricante">
                      <div className="flex gap-1.5">
                        <select value={form.manufacturerId ?? ''} onChange={(e) => set('manufacturerId', e.target.value || undefined)} className={`${inputCls} flex-1`}>
                          <option value="">— Selecione —</option>
                          {manufacturers.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
                        </select>
                        <button type="button" onClick={() => setShowNewManufacturer(true)} title="Cadastrar novo fabricante"
                          className="shrink-0 rounded-md border border-gray-300 dark:border-brand-700/50 bg-white dark:bg-brand-950/50 px-3 text-gray-500 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-brand-800/30 text-lg leading-none">
                          +
                        </button>
                      </div>
                    </Field>
                    <Field label="Fornecedor">
                      <select value={form.supplierId ?? ''} onChange={(e) => set('supplierId', e.target.value || undefined)} className={inputCls}>
                        <option value="">— Selecione —</option>
                        {suppliers.map((s) => <option key={s.id} value={s.id}>{s.tradeName ?? s.companyName}</option>)}
                      </select>
                    </Field>
                  </div>
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <Field label="Código de barras" hint="EAN-13 / EAN-8">
                      <input value={form.barcode ?? ''} onChange={(e) => set('barcode', e.target.value)} placeholder="Ex: 7891234567890" className={inputCls} />
                    </Field>
                    <Field label="Unidade de medida">
                      <select value={form.unit} onChange={(e) => set('unit', e.target.value)} className={inputCls}>
                        {UNITS.map((u) => <option key={u} value={u}>{u}</option>)}
                      </select>
                    </Field>
                  </div>
                  <Field label="Observações">
                    <textarea value={form.notes ?? ''} onChange={(e) => set('notes', e.target.value)}
                      rows={2} placeholder="Informações adicionais sobre o produto..." className={`${inputCls} resize-none`} />
                  </Field>
                </section>

                <div className="border-t border-gray-200 dark:border-brand-800/40" />

                {/* Preços */}
                <section className="space-y-4">
                  <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Preços</h3>
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <Field label="Preço de custo (R$)" required>
                      <input type="number" min="0" step="0.01" value={form.costPrice} onChange={numericInput('costPrice')} className={inputCls} />
                    </Field>
                    <Field label="Preço de venda (R$)" required>
                      <input type="number" min="0" step="0.01" value={form.salePrice} onChange={numericInput('salePrice')} className={inputCls} />
                    </Field>
                  </div>
                  {(form.costPrice ?? 0) > 0 && (form.salePrice ?? 0) > 0 && (
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      Margem:{' '}
                      <span className={`font-semibold ${(form.salePrice ?? 0) >= (form.costPrice ?? 0) ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                        {((((form.salePrice ?? 0) - (form.costPrice ?? 0)) / (form.costPrice ?? 1)) * 100).toFixed(1)}%
                      </span>
                    </p>
                  )}
                </section>

                <div className="border-t border-gray-200 dark:border-brand-800/40" />

                {/* Estoque */}
                <section className="space-y-4">
                  <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">Estoque</h3>
                  <Field label="Estoque mínimo" hint="Alerta quando disponível ≤ mínimo.">
                    <input type="number" min="0" step="0.01" value={form.stockMinimum} onChange={numericInput('stockMinimum')}
                      className={`${inputCls} max-w-48`} />
                  </Field>
                </section>

                <div className="border-t border-gray-200 dark:border-brand-800/40" />

                {/* Fiscal */}
                <section className="space-y-4">
                  <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
                    Dados fiscais <span className="font-normal normal-case text-gray-400">(opcional)</span>
                  </h3>
                  <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                    <Field label="NCM"><input value={form.ncm ?? ''} onChange={(e) => set('ncm', e.target.value)} placeholder="0000.00.00" className={inputCls} /></Field>
                    <Field label="CST"><input value={form.cst ?? ''} onChange={(e) => set('cst', e.target.value)} placeholder="000" className={inputCls} /></Field>
                    <Field label="ICMS (%)"><input type="number" min="0" max="100" step="0.01" value={form.icmsRate ?? ''} onChange={numericInput('icmsRate')} placeholder="0.00" className={inputCls} /></Field>
                    <Field label="IPI (%)"><input type="number" min="0" max="100" step="0.01" value={form.ipiRate ?? ''} onChange={numericInput('ipiRate')} placeholder="0.00" className={inputCls} /></Field>
                  </div>
                </section>

                {!isEdit && (
                  <p className="text-xs text-gray-400 dark:text-gray-500 text-right">
                    O SKU será gerado automaticamente após o cadastro.
                  </p>
                )}
              </div>

              {/* Footer */}
              <div className="flex justify-end gap-3 border-t border-gray-200 dark:border-brand-800/50 px-5 py-4 shrink-0">
                <button type="button" onClick={onClose}
                  className="rounded-md border border-gray-300 dark:border-brand-700/50 px-5 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-brand-800/30">
                  Cancelar
                </button>
                <button type="submit" disabled={saving}
                  className="rounded-md bg-brand-600 hover:bg-brand-700 px-5 py-2 text-sm font-medium text-white disabled:opacity-50">
                  {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Criar produto'}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>

      {showNewManufacturer && (
        <NewManufacturerModal
          onClose={() => setShowNewManufacturer(false)}
          onCreate={(m) => {
            setManufacturers((prev) => [...prev, m].sort((a, b) => a.name.localeCompare(b.name)));
            set('manufacturerId', m.id);
            setShowNewManufacturer(false);
          }}
        />
      )}
    </>
  );
}

// ─── Product list page ─────────────────────────────────────────────────────────

export function ProductListPage() {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'admin';

  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [modal, setModal] = useState<string | 'new' | null>(null);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadProducts = async (currentPage: number, currentSearch: string) => {
    try {
      setLoading(true);
      const data = await productService.getAll({
        page: currentPage,
        pageSize: PAGE_SIZE,
        search: currentSearch || undefined,
      });
      setProducts(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      toast.error('Erro ao carregar produtos.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProducts(page, search);
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      loadProducts(1, search);
    }, 400);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [search]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleDeactivate = async (id: string, name: string) => {
    if (!confirm(`Desativar produto "${name}"?`)) return;
    try {
      await productService.delete(id);
      toast.success('Produto desativado.');
      loadProducts(page, search);
    } catch {
      toast.error('Erro ao desativar produto.');
    }
  };

  const handleHardDelete = async (id: string, name: string) => {
    if (!confirm(`Excluir permanentemente o produto "${name}"?\n\nEsta ação não pode ser desfeita.`)) return;
    try {
      await productService.hardDelete(id);
      toast.success('Produto excluído.');
      loadProducts(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este produto.');
    }
  };

  const stockStatus = (p: Product) => {
    const available = p.stockQuantity - p.stockReserved;
    if (available <= 0) return 'text-red-600 dark:text-red-400 font-semibold';
    if (available <= p.stockMinimum) return 'text-yellow-600 dark:text-yellow-400 font-semibold';
    return 'text-green-700 dark:text-green-400';
  };

  return (
    <>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Produtos</h1>
          {isAdmin && (
            <button
              onClick={() => setModal('new')}
              className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
            >
              Novo produto
            </button>
          )}
        </div>

        <input
          type="text"
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Buscar por SKU, nome, código de barras, fabricante..."
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
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">SKU</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Nome</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Preço</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">Estoque disp.</th>
                    <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Status</th>
                    {isAdmin && <th className="px-4 py-3" />}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-950">
                  {products.length === 0 ? (
                    <tr>
                      <td colSpan={isAdmin ? 6 : 5} className="py-8 text-center text-gray-400 dark:text-gray-500">
                        Nenhum produto encontrado.
                      </td>
                    </tr>
                  ) : (
                    products.map(p => (
                      <tr key={p.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20">
                        <td className="px-4 py-3 font-mono text-sm text-gray-700 dark:text-gray-300">{p.sku}</td>
                        <td className="px-4 py-3">
                          <div className="font-medium text-gray-900 dark:text-white">{p.name}</div>
                          {p.manufacturerName && <div className="text-xs text-gray-400 dark:text-gray-500">{p.manufacturerName}</div>}
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">
                          R$ {p.salePrice.toFixed(2)}
                        </td>
                        <td className={`px-4 py-3 text-sm hidden sm:table-cell ${stockStatus(p)}`}>
                          {(p.stockQuantity - p.stockReserved).toFixed(0)} {p.unit}
                          {p.stockReserved > 0 && (
                            <span className="ml-1 text-xs text-gray-400 dark:text-gray-500">({p.stockReserved} res.)</span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                            p.isActive
                              ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                              : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                          }`}>
                            {p.isActive ? 'Ativo' : 'Inativo'}
                          </span>
                        </td>
                        {isAdmin && (
                          <td className="px-4 py-3 text-right whitespace-nowrap">
                            <div className="inline-flex items-center gap-1">
                              <button
                                onClick={() => setModal(p.id)}
                                title="Editar"
                                className="rounded p-1.5 text-gray-400 hover:bg-brand-50 hover:text-brand-600 dark:hover:bg-brand-900/30 dark:hover:text-brand-400 transition-colors"
                              >
                                <Pencil size={15} />
                              </button>
                              {p.isActive && (
                                <button
                                  onClick={() => handleDeactivate(p.id, p.name)}
                                  title="Desativar"
                                  className="rounded p-1.5 text-gray-400 hover:bg-amber-50 hover:text-amber-600 dark:hover:bg-amber-900/30 dark:hover:text-amber-400 transition-colors"
                                >
                                  <PowerOff size={15} />
                                </button>
                              )}
                              <button
                                onClick={() => handleHardDelete(p.id, p.name)}
                                title="Excluir permanentemente"
                                className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/30 dark:hover:text-red-400 transition-colors"
                              >
                                <Trash2 size={15} />
                              </button>
                            </div>
                          </td>
                        )}
                      </tr>
                    ))
                  )}
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
        <ProductModal
          productId={modal}
          onClose={() => setModal(null)}
          onSaved={() => loadProducts(page, search)}
        />
      )}
    </>
  );
}
