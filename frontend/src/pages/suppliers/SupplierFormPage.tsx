import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { supplierService, type CreateSupplierRequest } from '@/services/supplierService';

// ─── helpers ─────────────────────────────────────────────────────────────────

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

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

// ─── main ─────────────────────────────────────────────────────────────────────

export function SupplierFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();

  const [form, setForm] = useState<CreateSupplierRequest>(EMPTY);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isEdit) return;
    supplierService.getById(id!).then(s => {
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
      navigate('/suppliers');
    });
  }, [id, isEdit, navigate]);

  function set<K extends keyof CreateSupplierRequest>(key: K, value: CreateSupplierRequest[K]) {
    setForm(prev => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!form.companyName.trim()) {
      toast.error('Razão social é obrigatória.');
      return;
    }
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
        await supplierService.update(id!, payload);
        toast.success('Fornecedor atualizado.');
      } else {
        await supplierService.create(payload);
        toast.success('Fornecedor cadastrado.');
      }
      navigate('/suppliers');
    } catch {
      toast.error('Erro ao salvar fornecedor.');
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
          type="button"
          onClick={() => navigate('/suppliers')}
          className="rounded-md p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600 dark:hover:text-gray-200"
        >
          ←
        </button>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
          {isEdit ? 'Editar Fornecedor' : 'Novo Fornecedor'}
        </h1>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">

        {/* ── Identificação ─────────────────────────────────────────────── */}
        <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Identificação
          </h2>
          <Field label="Razão social" required>
            <input
              value={form.companyName}
              onChange={e => set('companyName', e.target.value)}
              placeholder="Ex: Distribuidora Moto Peças Ltda"
              className={inputCls}
            />
          </Field>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="Nome fantasia">
              <input
                value={form.tradeName ?? ''}
                onChange={e => set('tradeName', e.target.value)}
                placeholder="Ex: MotoDistrib"
                className={inputCls}
              />
            </Field>
            <Field label="CNPJ">
              <input
                value={form.cnpj ?? ''}
                onChange={e => set('cnpj', e.target.value)}
                placeholder="00.000.000/0001-00"
                className={inputCls}
              />
            </Field>
          </div>
        </section>

        {/* ── Contato ───────────────────────────────────────────────────── */}
        <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Contato
          </h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Field label="Telefone">
              <input
                value={form.phone ?? ''}
                onChange={e => set('phone', e.target.value)}
                placeholder="(00) 3000-0000"
                className={inputCls}
              />
            </Field>
            <Field label="E-mail">
              <input
                type="email"
                value={form.email ?? ''}
                onChange={e => set('email', e.target.value)}
                placeholder="contato@fornecedor.com"
                className={inputCls}
              />
            </Field>
            <Field label="Pessoa de contato">
              <input
                value={form.contactPerson ?? ''}
                onChange={e => set('contactPerson', e.target.value)}
                placeholder="Nome do responsável"
                className={inputCls}
              />
            </Field>
          </div>
        </section>

        {/* ── Endereço + Obs ─────────────────────────────────────────────── */}
        <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Endereço e observações
          </h2>
          <Field label="Endereço">
            <input
              value={form.address ?? ''}
              onChange={e => set('address', e.target.value)}
              placeholder="Rua, número, bairro, cidade / UF"
              className={inputCls}
            />
          </Field>
          <Field label="Observações">
            <textarea
              value={form.notes ?? ''}
              onChange={e => set('notes', e.target.value)}
              rows={3}
              placeholder="Condições comerciais, prazo de entrega, etc."
              className={`${inputCls} resize-none`}
            />
          </Field>
        </section>

        {/* ── Actions ───────────────────────────────────────────────────── */}
        <div className="flex justify-end gap-3 pb-4">
          <button
            type="button"
            onClick={() => navigate('/suppliers')}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-5 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            Cancelar
          </button>
          <button
            type="submit"
            disabled={saving}
            className="rounded-md bg-brand-600 hover:bg-brand-700 px-5 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Cadastrar fornecedor'}
          </button>
        </div>
      </form>
    </div>
  );
}
