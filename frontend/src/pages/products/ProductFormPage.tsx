import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { productService } from '@/services/productService';
import { manufacturerService } from '@/services/manufacturerService';
import { supplierService, type SupplierOption } from '@/services/supplierService';
import type { CreateProductRequest } from '@/types/product';
import type { Manufacturer } from '@/types/manufacturer';

// ─── constants ───────────────────────────────────────────────────────────────

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

// ─── helpers ─────────────────────────────────────────────────────────────────

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

function Field({
  label,
  required,
  hint,
  children,
}: {
  label: string;
  required?: boolean;
  hint?: string;
  children: React.ReactNode;
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

// ─── quick-add manufacturer modal ────────────────────────────────────────────

interface NewManufacturerModalProps {
  onClose: () => void;
  onCreate: (m: Manufacturer) => void;
}

function NewManufacturerModal({ onClose, onCreate }: NewManufacturerModalProps) {
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
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="w-full max-w-sm rounded-xl bg-white dark:bg-gray-900 shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 dark:border-gray-700 px-5 py-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">Novo Fabricante</h2>
          <button
            onClick={onClose}
            className="rounded-md p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600 dark:hover:text-gray-200"
          >
            ✕
          </button>
        </div>
        <div className="p-5 space-y-4">
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
              className={`${inputCls} resize-none`}
            />
          </Field>
          <div className="flex gap-2 justify-end pt-1">
            <button
              onClick={onClose}
              className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
            >
              Cancelar
            </button>
            <button
              onClick={handleSave}
              disabled={saving}
              className="rounded-md bg-brand-600 hover:bg-brand-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
            >
              {saving ? 'Salvando...' : 'Cadastrar'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── main form ───────────────────────────────────────────────────────────────

export function ProductFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();

  const [form, setForm] = useState<CreateProductRequest>(EMPTY);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  const [manufacturers, setManufacturers] = useState<Manufacturer[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierOption[]>([]);
  const [showNewManufacturer, setShowNewManufacturer] = useState(false);

  // Load select options
  useEffect(() => {
    manufacturerService.getActive().then(setManufacturers).catch(() => {});
    supplierService.getAllForSelect().then(setSuppliers).catch(() => {});
  }, []);

  // Load product for edit
  useEffect(() => {
    if (!isEdit) return;
    productService.getById(id!).then((p) => {
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
      navigate('/products');
    });
  }, [id, isEdit, navigate]);

  function set<K extends keyof CreateProductRequest>(key: K, value: CreateProductRequest[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function numericInput(key: keyof CreateProductRequest) {
    return (e: React.ChangeEvent<HTMLInputElement>) => {
      const v = e.target.value === '' ? undefined : parseFloat(e.target.value);
      setForm((prev) => ({ ...prev, [key]: v }));
    };
  }

  function handleNewManufacturerCreated(m: Manufacturer) {
    setManufacturers((prev) => [...prev, m].sort((a, b) => a.name.localeCompare(b.name)));
    set('manufacturerId', m.id);
    setShowNewManufacturer(false);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim()) {
      toast.error('Nome do produto é obrigatório.');
      return;
    }
    setSaving(true);
    try {
      if (isEdit) {
        await productService.update(id!, form);
        toast.success('Produto atualizado.');
      } else {
        await productService.create(form);
        toast.success('Produto criado.');
      }
      navigate('/products');
    } catch {
      toast.error('Erro ao salvar produto.');
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <div className="py-16 text-center text-gray-500 dark:text-gray-400">Carregando...</div>;
  }

  return (
    <>
      <div className="mx-auto max-w-3xl space-y-6">
        {/* Header */}
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate('/products')}
            className="rounded-md p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600 dark:hover:text-gray-200"
            title="Voltar"
          >
            ←
          </button>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            {isEdit ? 'Editar Produto' : 'Novo Produto'}
          </h1>
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* ── Identificação ─────────────────────────────────────────────── */}
          <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
              Identificação
            </h2>

            <Field label="Nome do produto" required>
              <input
                value={form.name}
                onChange={(e) => set('name', e.target.value)}
                placeholder="Ex: Pneu Traseiro 90/90-18 Pirelli"
                className={inputCls}
              />
            </Field>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {/* Manufacturer select + quick add */}
              <Field label="Fabricante">
                <div className="flex gap-1.5">
                  <select
                    value={form.manufacturerId ?? ''}
                    onChange={(e) => set('manufacturerId', e.target.value || undefined)}
                    className={`${inputCls} flex-1`}
                  >
                    <option value="">— Selecione —</option>
                    {manufacturers.map((m) => (
                      <option key={m.id} value={m.id}>{m.name}</option>
                    ))}
                  </select>
                  <button
                    type="button"
                    onClick={() => setShowNewManufacturer(true)}
                    title="Cadastrar novo fabricante"
                    className="shrink-0 rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 text-gray-500 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-700 hover:text-gray-700 dark:hover:text-gray-200 text-lg leading-none"
                  >
                    +
                  </button>
                </div>
              </Field>

              {/* Supplier select */}
              <Field label="Fornecedor">
                <select
                  value={form.supplierId ?? ''}
                  onChange={(e) => set('supplierId', e.target.value || undefined)}
                  className={inputCls}
                >
                  <option value="">— Selecione —</option>
                  {suppliers.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.tradeName ?? s.companyName}
                    </option>
                  ))}
                </select>
              </Field>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label="Código de barras" hint="EAN-13 / EAN-8">
                <input
                  value={form.barcode ?? ''}
                  onChange={(e) => set('barcode', e.target.value)}
                  placeholder="Ex: 7891234567890"
                  className={inputCls}
                />
              </Field>
              <Field label="Unidade de medida">
                <select
                  value={form.unit}
                  onChange={(e) => set('unit', e.target.value)}
                  className={inputCls}
                >
                  {UNITS.map((u) => (
                    <option key={u} value={u}>{u}</option>
                  ))}
                </select>
              </Field>
            </div>

            <Field label="Observações">
              <textarea
                value={form.notes ?? ''}
                onChange={(e) => set('notes', e.target.value)}
                rows={2}
                placeholder="Informações adicionais sobre o produto..."
                className={`${inputCls} resize-none`}
              />
            </Field>
          </section>

          {/* ── Preços ────────────────────────────────────────────────────── */}
          <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
              Preços
            </h2>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label="Preço de custo (R$)" required>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.costPrice}
                  onChange={numericInput('costPrice')}
                  className={inputCls}
                />
              </Field>
              <Field label="Preço de venda (R$)" required>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.salePrice}
                  onChange={numericInput('salePrice')}
                  className={inputCls}
                />
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

          {/* ── Estoque ───────────────────────────────────────────────────── */}
          <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
              Estoque
            </h2>
            <Field
              label="Estoque mínimo"
              hint="Alerta de estoque baixo quando disponível ≤ mínimo."
            >
              <input
                type="number"
                min="0"
                step="0.01"
                value={form.stockMinimum}
                onChange={numericInput('stockMinimum')}
                className={`${inputCls} max-w-48`}
              />
            </Field>
          </section>

          {/* ── Fiscal ────────────────────────────────────────────────────── */}
          <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
              Dados fiscais <span className="font-normal normal-case text-gray-400">(opcional)</span>
            </h2>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
              <Field label="NCM">
                <input
                  value={form.ncm ?? ''}
                  onChange={(e) => set('ncm', e.target.value)}
                  placeholder="0000.00.00"
                  className={inputCls}
                />
              </Field>
              <Field label="CST">
                <input
                  value={form.cst ?? ''}
                  onChange={(e) => set('cst', e.target.value)}
                  placeholder="000"
                  className={inputCls}
                />
              </Field>
              <Field label="ICMS (%)">
                <input
                  type="number"
                  min="0"
                  max="100"
                  step="0.01"
                  value={form.icmsRate ?? ''}
                  onChange={numericInput('icmsRate')}
                  placeholder="0.00"
                  className={inputCls}
                />
              </Field>
              <Field label="IPI (%)">
                <input
                  type="number"
                  min="0"
                  max="100"
                  step="0.01"
                  value={form.ipiRate ?? ''}
                  onChange={numericInput('ipiRate')}
                  placeholder="0.00"
                  className={inputCls}
                />
              </Field>
            </div>
          </section>

          {/* ── Info SKU ──────────────────────────────────────────────────── */}
          {!isEdit && (
            <p className="text-xs text-gray-400 dark:text-gray-500 text-right">
              O SKU será gerado automaticamente após o cadastro.
            </p>
          )}

          {/* ── Actions ───────────────────────────────────────────────────── */}
          <div className="flex justify-end gap-3 pb-4">
            <button
              type="button"
              onClick={() => navigate('/products')}
              className="rounded-md border border-gray-300 dark:border-gray-600 px-5 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={saving}
              className="rounded-md bg-brand-600 hover:bg-brand-700 px-5 py-2 text-sm font-medium text-white disabled:opacity-50"
            >
              {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Criar produto'}
            </button>
          </div>
        </form>
      </div>

      {showNewManufacturer && (
        <NewManufacturerModal
          onClose={() => setShowNewManufacturer(false)}
          onCreate={handleNewManufacturerCreated}
        />
      )}
    </>
  );
}
