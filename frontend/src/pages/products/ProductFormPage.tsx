import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { productService } from '@/services/productService';
import type { CreateProductRequest } from '@/types/product';

const UNITS = ['un', 'pc', 'par', 'kit', 'cx', 'kg', 'g', 'l', 'ml', 'm', 'cm'];

const EMPTY: CreateProductRequest = {
  sku: '',
  barcode: '',
  name: '',
  manufacturer: '',
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

function Field({
  label,
  required,
  children,
}: {
  label: string;
  required?: boolean;
  children: React.ReactNode;
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

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500';

export function ProductFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();

  const [form, setForm] = useState<CreateProductRequest>(EMPTY);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isEdit) return;
    productService.getById(id!).then((p) => {
      setForm({
        sku: p.sku,
        barcode: p.barcode ?? '',
        name: p.name,
        manufacturer: p.manufacturer ?? '',
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

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.sku.trim() || !form.name.trim()) {
      toast.error('SKU e nome são obrigatórios.');
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
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="SKU" required>
              <input
                value={form.sku}
                onChange={(e) => set('sku', e.target.value)}
                placeholder="Ex: MOT-001"
                className={inputCls}
              />
            </Field>
            <Field label="Código de barras">
              <input
                value={form.barcode ?? ''}
                onChange={(e) => set('barcode', e.target.value)}
                placeholder="EAN-13 / EAN-8"
                className={inputCls}
              />
            </Field>
          </div>
          <Field label="Nome do produto" required>
            <input
              value={form.name}
              onChange={(e) => set('name', e.target.value)}
              placeholder="Ex: Pneu Traseiro 90/90-18 Pirelli"
              className={inputCls}
            />
          </Field>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="Fabricante / Marca">
              <input
                value={form.manufacturer ?? ''}
                onChange={(e) => set('manufacturer', e.target.value)}
                placeholder="Ex: Pirelli"
                className={inputCls}
              />
            </Field>
            <Field label="Unidade">
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
          {form.costPrice > 0 && form.salePrice > 0 && (
            <p className="text-xs text-gray-500 dark:text-gray-400">
              Margem:{' '}
              <span className={`font-semibold ${form.salePrice >= form.costPrice ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                {(((form.salePrice - form.costPrice) / form.costPrice) * 100).toFixed(1)}%
              </span>
            </p>
          )}
        </section>

        {/* ── Estoque ───────────────────────────────────────────────────── */}
        <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Estoque
          </h2>
          <Field label="Estoque mínimo" required>
            <input
              type="number"
              min="0"
              step="0.01"
              value={form.stockMinimum}
              onChange={numericInput('stockMinimum')}
              className={`${inputCls} max-w-48`}
            />
            <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">
              Alerta de estoque baixo quando disponível ≤ mínimo.
            </p>
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
            className="rounded-md bg-blue-600 hover:bg-blue-700 px-5 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Criar produto'}
          </button>
        </div>
      </form>
    </div>
  );
}
