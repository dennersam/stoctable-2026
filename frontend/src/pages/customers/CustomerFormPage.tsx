import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { customerService } from '@/services/customerService';
import type { CreateCustomerRequest } from '@/types/customer';

// ─── helpers ─────────────────────────────────────────────────────────────────

const inputCls =
  'w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500';

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

export function CustomerFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const navigate = useNavigate();

  const [form, setForm] = useState<CreateCustomerRequest>(EMPTY);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isEdit) return;
    customerService.getById(id!).then((c) => {
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
      navigate('/customers');
    });
  }, [id, isEdit, navigate]);

  function set<K extends keyof CreateCustomerRequest>(key: K, value: CreateCustomerRequest[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!form.fullName.trim()) {
      toast.error('Nome é obrigatório.');
      return;
    }
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
        await customerService.update(id!, payload);
        toast.success('Cliente atualizado.');
      } else {
        await customerService.create(payload);
        toast.success('Cliente cadastrado.');
      }
      navigate('/customers');
    } catch {
      toast.error('Erro ao salvar cliente.');
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
          onClick={() => navigate('/customers')}
          className="rounded-md p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600 dark:hover:text-gray-200"
        >
          ←
        </button>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
          {isEdit ? 'Editar Cliente' : 'Novo Cliente'}
        </h1>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">

        {/* ── Dados pessoais ────────────────────────────────────────────── */}
        <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Dados pessoais
          </h2>

          <Field label="Nome completo / Razão social" required>
            <input
              value={form.fullName}
              onChange={e => set('fullName', e.target.value)}
              placeholder="Ex: João da Silva"
              className={inputCls}
            />
          </Field>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Field label="Tipo de documento">
              <select
                value={form.documentType}
                onChange={e => set('documentType', e.target.value as 'CPF' | 'CNPJ')}
                className={inputCls}
              >
                <option value="CPF">CPF</option>
                <option value="CNPJ">CNPJ</option>
              </select>
            </Field>
            <Field label={form.documentType === 'CPF' ? 'CPF' : 'CNPJ'}>
              <input
                value={form.documentNumber ?? ''}
                onChange={e => set('documentNumber', e.target.value)}
                placeholder={form.documentType === 'CPF' ? '000.000.000-00' : '00.000.000/0001-00'}
                className={inputCls}
              />
            </Field>
            <Field label="Limite de crédito (R$)">
              <input
                type="number"
                min="0"
                step="0.01"
                value={form.creditLimit ?? 0}
                onChange={e => set('creditLimit', parseFloat(e.target.value) || 0)}
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
            <Field label="E-mail">
              <input
                type="email"
                value={form.email ?? ''}
                onChange={e => set('email', e.target.value)}
                placeholder="email@exemplo.com"
                className={inputCls}
              />
            </Field>
            <Field label="Telefone">
              <input
                value={form.phone ?? ''}
                onChange={e => set('phone', e.target.value)}
                placeholder="(00) 3000-0000"
                className={inputCls}
              />
            </Field>
            <Field label="Celular / WhatsApp">
              <input
                value={form.mobile ?? ''}
                onChange={e => set('mobile', e.target.value)}
                placeholder="(00) 90000-0000"
                className={inputCls}
              />
            </Field>
          </div>
        </section>

        {/* ── Endereço ──────────────────────────────────────────────────── */}
        <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Endereço
          </h2>
          <Field label="Logradouro">
            <input
              value={form.address ?? ''}
              onChange={e => set('address', e.target.value)}
              placeholder="Rua, número, complemento"
              className={inputCls}
            />
          </Field>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Field label="Cidade">
              <input
                value={form.city ?? ''}
                onChange={e => set('city', e.target.value)}
                placeholder="Ex: São Paulo"
                className={inputCls}
              />
            </Field>
            <Field label="Estado">
              <select
                value={form.state ?? ''}
                onChange={e => set('state', e.target.value)}
                className={inputCls}
              >
                <option value="">— UF —</option>
                {STATES.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
            </Field>
            <Field label="CEP">
              <input
                value={form.zipCode ?? ''}
                onChange={e => set('zipCode', e.target.value)}
                placeholder="00000-000"
                className={inputCls}
              />
            </Field>
          </div>
        </section>

        {/* ── Observações ───────────────────────────────────────────────── */}
        <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
            Observações
          </h2>
          <textarea
            value={form.notes ?? ''}
            onChange={e => set('notes', e.target.value)}
            rows={3}
            placeholder="Informações adicionais sobre o cliente..."
            className={`${inputCls} resize-none`}
          />
        </section>

        {/* ── Actions ───────────────────────────────────────────────────── */}
        <div className="flex justify-end gap-3 pb-4">
          <button
            type="button"
            onClick={() => navigate('/customers')}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-5 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            Cancelar
          </button>
          <button
            type="submit"
            disabled={saving}
            className="rounded-md bg-blue-600 hover:bg-blue-700 px-5 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {saving ? 'Salvando...' : isEdit ? 'Salvar alterações' : 'Cadastrar cliente'}
          </button>
        </div>
      </form>
    </div>
  );
}
