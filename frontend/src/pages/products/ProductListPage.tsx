import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { AlertTriangle, PackageX, Pencil, Plus, PowerOff, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field, FormSection, SearchField } from '@/components/ui/Field';
import { fieldClass as inputCls } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHeader';
import { RowActions, RowActionsMenu, type RowAction } from '@/components/ui/RowActions';
import { Badge } from '@/components/ui/Badge';
import { confirmDialog } from '@/lib/dialogs';
import { productService } from '@/services/productService';
import { manufacturerService } from '@/services/manufacturerService';
import { supplierService, type SupplierOption } from '@/services/supplierService';
import type { Product, CreateProductRequest } from '@/types/product';
import type { Manufacturer } from '@/types/manufacturer';
import { useAuthStore } from '@/store/authStore';
import { Pagination } from '@/components/base/Pagination';
import { CurrencyInput } from '@/components/ui/CurrencyInput';
import { formatCurrency } from '@/lib/utils';

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
    <Modal title="Novo fabricante" onClose={onClose} size="sm">
      <ModalBody className="space-y-4">
        <Field label="Nome" required>
          <input autoFocus value={name} onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSave()}
            placeholder="Ex: Pirelli, Honda, NGK..." className={inputCls} />
        </Field>
        <Field label="Observações">
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)}
            rows={2} placeholder="Informações adicionais (opcional)"
            className={inputCls + ' resize-none'} />
        </Field>
      </ModalBody>
      <ModalFooter>
        <Button variant="outline" onClick={onClose}>Cancelar</Button>
        <Button onClick={handleSave} disabled={saving}>{saving ? 'Salvando...' : 'Cadastrar fabricante'}</Button>
      </ModalFooter>
    </Modal>
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
        <Modal title={isEdit ? 'Editar produto' : 'Novo produto'} onClose={onClose} size="xl">
          {loading ? (
            <div role="status" className="flex-1 py-16 text-center text-sm text-gray-500 dark:text-gray-400">Carregando...</div>
          ) : (
            <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
              <ModalBody className="space-y-5">

                {/* Identificação */}
                <FormSection title="Identificação">
                  <Field label="Nome do produto" required>
                    <input value={form.name} onChange={(e) => set('name', e.target.value)}
                      placeholder="Ex: Pneu Traseiro 90/90-18 Pirelli" className={inputCls} />
                  </Field>
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <Field label="Fabricante">
                      <div className="flex gap-1.5">
                        <select value={form.manufacturerId ?? ''} onChange={(e) => set('manufacturerId', e.target.value || undefined)} className={inputCls + ' flex-1'}>
                          <option value="">— Selecione —</option>
                          {manufacturers.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
                        </select>
                        <button type="button" onClick={() => setShowNewManufacturer(true)} title="Cadastrar novo fabricante"
                          aria-label="Cadastrar novo fabricante" className="flex w-11 shrink-0 items-center justify-center rounded-md border border-gray-300 dark:border-white/15 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-white/5 md:w-9">
                          <Plus size={16} />
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
                </FormSection>

                <div className="border-t border-line" />

                {/* Preços */}
                <FormSection title="Preços">
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <Field label="Preço de custo" required>
                      <CurrencyInput value={form.costPrice} onValueChange={(v) => set('costPrice', v)} className={inputCls} />
                    </Field>
                    <Field label="Preço de venda" required>
                      <CurrencyInput value={form.salePrice} onValueChange={(v) => set('salePrice', v)} className={inputCls} />
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
                </FormSection>

                <div className="border-t border-line" />

                {/* Estoque */}
                <FormSection title="Estoque">
                  <Field label="Estoque mínimo nesta filial" hint="Vale para a filial ativa. Cada loja define o seu.">
                    <input type="number" min="0" step="0.01" value={form.stockMinimum} onChange={numericInput('stockMinimum')}
                      className={`${inputCls} max-w-48`} />
                  </Field>
                </FormSection>

                <div className="border-t border-line" />

                {/* Fiscal */}
                <FormSection title={<>Dados fiscais <span className="font-normal text-gray-500 dark:text-gray-400">(opcional)</span></>}>
                  <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                    <Field label="NCM"><input value={form.ncm ?? ''} onChange={(e) => set('ncm', e.target.value)} placeholder="0000.00.00" className={inputCls} /></Field>
                    <Field label="CST"><input value={form.cst ?? ''} onChange={(e) => set('cst', e.target.value)} placeholder="000" className={inputCls} /></Field>
                    <Field label="ICMS (%)"><input type="number" min="0" max="100" step="0.01" value={form.icmsRate ?? ''} onChange={numericInput('icmsRate')} placeholder="0.00" className={inputCls} /></Field>
                    <Field label="IPI (%)"><input type="number" min="0" max="100" step="0.01" value={form.ipiRate ?? ''} onChange={numericInput('ipiRate')} placeholder="0.00" className={inputCls} /></Field>
                  </div>
                </FormSection>

                {!isEdit && (
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    O SKU será gerado automaticamente após o cadastro.
                  </p>
                )}
              </ModalBody>

              <ModalFooter>
                <Button type="button" variant="outline" onClick={onClose}>Cancelar</Button>
                <Button type="submit" disabled={saving}>
                  {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Criar produto'}
                </Button>
              </ModalFooter>
            </form>
          )}
        </Modal>

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

// ─── Partes da lista ───────────────────────────────────────────────────────────

function ProductName({ product: p, showSku = false }: { product: Product; showSku?: boolean }) {
  const detail = [showSku ? p.sku : null, p.manufacturerName].filter(Boolean).join(' · ');
  return (
    <>
      <div className="font-medium text-gray-900 dark:text-white">
        {p.name}
        {!p.isActive && <Badge variant="neutral" className="ml-2 align-middle">Inativo</Badge>}
      </div>
      {detail && <div className="text-xs text-gray-500 dark:text-gray-400">{detail}</div>}
    </>
  );
}

/** Saldo com ícone além da cor: zerado e baixo precisam se distinguir sem depender dela. */
function StockLevel({ product: p }: { product: Product }) {
  const available = p.stockQuantity - p.stockReserved;
  const out = available <= 0;
  const low = !out && available <= p.stockMinimum;
  const Icon = out ? PackageX : low ? AlertTriangle : null;

  return (
    <span className={'inline-flex items-center gap-1 ' + (out
      ? 'font-semibold text-red-700 dark:text-red-400'
      : low ? 'font-semibold text-amber-700 dark:text-amber-400' : 'text-gray-700 dark:text-gray-300')}
    >
      {Icon && <Icon size={14} aria-hidden="true" />}
      {available.toFixed(0)} {p.unit}
      {out && <span className="font-normal">(zerado)</span>}
      {p.stockReserved > 0 && (
        <span className="text-xs font-normal text-gray-500 dark:text-gray-400">({p.stockReserved} res.)</span>
      )}
    </span>
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
    if (!(await confirmDialog({ title: 'Desativar produto', description: `"${name}" ficará inativo.`, confirmLabel: 'Desativar produto' }))) return;
    try {
      await productService.delete(id);
      toast.success('Produto desativado.');
      loadProducts(page, search);
    } catch {
      toast.error('Erro ao desativar produto.');
    }
  };

  const handleHardDelete = async (id: string, name: string) => {
    if (!(await confirmDialog({ title: 'Excluir produto', description: `"${name}" será excluído permanentemente. Esta ação não pode ser desfeita.`, confirmLabel: 'Excluir produto', tone: 'danger' }))) return;
    try {
      await productService.hardDelete(id);
      toast.success('Produto excluído.');
      loadProducts(page, search);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não é possível excluir este produto.');
    }
  };

  const actionsFor = (p: Product): RowAction[] => [
    { label: 'Editar', icon: Pencil, onClick: () => setModal(p.id) },
    { label: 'Desativar', icon: PowerOff, tone: 'warning', hidden: !p.isActive, onClick: () => handleDeactivate(p.id, p.name) },
    { label: 'Excluir permanentemente', icon: Trash2, tone: 'danger', onClick: () => handleHardDelete(p.id, p.name) },
  ];

  const columns: Column<Product>[] = [
    { key: 'sku', header: 'SKU', cell: (p) => <span className="font-mono text-xs whitespace-nowrap">{p.sku}</span> },
    { key: 'name', header: 'Nome', cell: (p) => <ProductName product={p} /> },
    { key: 'price', header: 'Preço', align: 'right', className: 'whitespace-nowrap', cell: (p) => formatCurrency(p.salePrice) },
    { key: 'stock', header: 'Disponível', align: 'right', className: 'whitespace-nowrap', cell: (p) => <StockLevel product={p} /> },
    ...(isAdmin
      ? [{ key: 'actions', header: '', align: 'right' as const, cell: (p: Product) => <RowActions actions={actionsFor(p)} /> }]
      : []),
  ];

  return (
    <>
      <div className="space-y-4">
        <PageHeader title="Produtos">
          {isAdmin && <Button onClick={() => setModal('new')}><Plus size={16} />Novo produto</Button>}
        </PageHeader>

        <SearchField
          value={search}
          onChange={setSearch}
          placeholder="Buscar por nome, SKU, código ou fabricante"
        />

        <DataTable
          caption="Produtos"
          rows={products}
          columns={columns}
          rowKey={(p) => p.id}
          loading={loading}
          empty={search ? 'Nenhum produto encontrado para esta busca.' : 'Nenhum produto cadastrado ainda.'}
          card={(p) => (
            <>
              <div className="flex items-start gap-2">
                <div className="min-w-0 flex-1"><ProductName product={p} showSku /></div>
                {isAdmin && <RowActionsMenu actions={actionsFor(p)} subject={p.name} />}
              </div>
              <div className="mt-2 flex items-baseline justify-between gap-3">
                <span className="text-base font-semibold tabular-nums text-gray-900 dark:text-white">{formatCurrency(p.salePrice)}</span>
                <span className="text-sm tabular-nums"><StockLevel product={p} /></span>
              </div>
            </>
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
        <ProductModal
          productId={modal}
          onClose={() => setModal(null)}
          onSaved={() => loadProducts(page, search)}
        />
      )}
    </>
  );
}
