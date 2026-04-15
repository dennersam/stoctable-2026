import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { customerService } from '@/services/customerService';
import type { Customer, CustomerCrmNote } from '@/types/customer';
import { CustomerModal } from './CustomerListPage';

// ─── helpers ─────────────────────────────────────────────────────────────────

const NOTE_TYPE_LABEL: Record<CustomerCrmNote['noteType'], string> = {
  general: 'Geral',
  complaint: 'Reclamação',
  followup: 'Acompanhamento',
};

const NOTE_TYPE_COLOR: Record<CustomerCrmNote['noteType'], string> = {
  general: 'bg-brand-100 dark:bg-brand-900/40 text-brand-700 dark:text-brand-400',
  complaint: 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-400',
  followup: 'bg-yellow-100 dark:bg-yellow-900/40 text-yellow-700 dark:text-yellow-400',
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('pt-BR', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

function InfoRow({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <p className="text-xs text-gray-500 dark:text-gray-400">{label}</p>
      <p className="text-sm font-medium text-gray-900 dark:text-white mt-0.5">
        {value || <span className="text-gray-400 font-normal">—</span>}
      </p>
    </div>
  );
}

// ─── main page ───────────────────────────────────────────────────────────────

export function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [customer, setCustomer] = useState<Customer | null>(null);
  const [loading, setLoading] = useState(true);
  const [editOpen, setEditOpen] = useState(false);

  const [notes, setNotes] = useState<CustomerCrmNote[]>([]);
  const [notesLoading, setNotesLoading] = useState(true);

  const [newNote, setNewNote] = useState('');
  const [newNoteType, setNewNoteType] = useState<CustomerCrmNote['noteType']>('general');
  const [addingNote, setAddingNote] = useState(false);

  const loadCustomer = useCallback(async () => {
    if (!id) return;
    try {
      const c = await customerService.getById(id);
      setCustomer(c);
    } catch {
      toast.error('Erro ao carregar cliente.');
      navigate('/customers');
    } finally {
      setLoading(false);
    }
  }, [id, navigate]);

  const loadNotes = useCallback(async () => {
    if (!id) return;
    setNotesLoading(true);
    try {
      const data = await customerService.getCrmNotes(id);
      setNotes(data);
    } catch {
      // notas não críticas
    } finally {
      setNotesLoading(false);
    }
  }, [id]);

  useEffect(() => {
    loadCustomer();
    loadNotes();
  }, [loadCustomer, loadNotes]);

  async function handleAddNote(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!newNote.trim()) return;
    setAddingNote(true);
    try {
      await customerService.addCrmNote(id!, newNote.trim(), newNoteType);
      toast.success('Nota adicionada.');
      setNewNote('');
      setNewNoteType('general');
      loadNotes();
    } catch {
      toast.error('Erro ao adicionar nota.');
    } finally {
      setAddingNote(false);
    }
  }

  async function handleDeactivate() {
    if (!customer) return;
    if (!confirm(`Desativar cliente "${customer.fullName}"?`)) return;
    try {
      await customerService.update(id!, { ...customer, isActive: false } as any);
      toast.success('Cliente desativado.');
      navigate('/customers');
    } catch {
      toast.error('Erro ao desativar cliente.');
    }
  }

  if (loading) {
    return <div className="py-16 text-center text-gray-500 dark:text-gray-400">Carregando...</div>;
  }
  if (!customer) return null;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <button
            onClick={() => navigate('/customers')}
            className="shrink-0 rounded-md p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600 dark:hover:text-gray-200"
          >
            ←
          </button>
          <div className="min-w-0">
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white truncate">{customer.fullName}</h1>
            <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium mt-1 ${
              customer.isActive
                ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                : 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
            }`}>
              {customer.isActive ? 'Ativo' : 'Inativo'}
            </span>
          </div>
        </div>
        <div className="flex shrink-0 gap-2">
          <button
            onClick={() => setEditOpen(true)}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            Editar
          </button>
          {customer.isActive && (
            <button
              onClick={handleDeactivate}
              className="rounded-md border border-red-200 dark:border-red-800 px-4 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20"
            >
              Desativar
            </button>
          )}
        </div>
      </div>

      {/* ── Dados ─────────────────────────────────────────────────────── */}
      <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-5">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
          Dados do cliente
        </h2>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <InfoRow label="Documento" value={customer.documentNumber ? `${customer.documentType}: ${customer.documentNumber}` : undefined} />
          <InfoRow label="E-mail" value={customer.email} />
          <InfoRow label="Celular / WhatsApp" value={customer.mobile} />
          <InfoRow label="Telefone" value={customer.phone} />
          <InfoRow label="CEP" value={customer.zipCode} />
          <InfoRow label="Limite de crédito" value={customer.creditLimit > 0 ? `R$ ${customer.creditLimit.toFixed(2)}` : undefined} />
        </div>
        {(customer.address || customer.city) && (
          <InfoRow
            label="Endereço"
            value={[customer.address, customer.city, customer.state].filter(Boolean).join(', ')}
          />
        )}
        {customer.notes && (
          <InfoRow label="Observações" value={customer.notes} />
        )}
      </section>

      {/* ── CRM ───────────────────────────────────────────────────────── */}
      <section className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 space-y-4">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">
          Histórico CRM
        </h2>

        {/* Add note form */}
        <form onSubmit={handleAddNote} className="space-y-3">
          <textarea
            value={newNote}
            onChange={e => setNewNote(e.target.value)}
            rows={2}
            placeholder="Adicionar anotação sobre o cliente..."
            className="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"
          />
          <div className="flex items-center gap-2">
            <select
              value={newNoteType}
              onChange={e => setNewNoteType(e.target.value as CustomerCrmNote['noteType'])}
              className="rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              <option value="general">Geral</option>
              <option value="followup">Acompanhamento</option>
              <option value="complaint">Reclamação</option>
            </select>
            <button
              type="submit"
              disabled={addingNote || !newNote.trim()}
              className="rounded-md bg-brand-600 hover:bg-brand-700 px-4 py-1.5 text-sm font-medium text-white disabled:opacity-50"
            >
              {addingNote ? 'Salvando...' : 'Adicionar nota'}
            </button>
          </div>
        </form>

        {/* Notes list */}
        <div className="space-y-2 pt-2">
          {notesLoading ? (
            <p className="text-sm text-gray-400 dark:text-gray-500 text-center py-4">Carregando notas...</p>
          ) : notes.length === 0 ? (
            <p className="text-sm text-gray-400 dark:text-gray-500 text-center py-4">
              Nenhuma anotação registrada.
            </p>
          ) : notes.map(n => (
            <div
              key={n.id}
              className="rounded-lg border border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-4 py-3 flex gap-3"
            >
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1">
                  <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${NOTE_TYPE_COLOR[n.noteType as CustomerCrmNote['noteType']] ?? NOTE_TYPE_COLOR.general}`}>
                    {NOTE_TYPE_LABEL[n.noteType as CustomerCrmNote['noteType']] ?? n.noteType}
                  </span>
                </div>
                <p className="text-sm text-gray-800 dark:text-gray-200 whitespace-pre-wrap">{n.note}</p>
              </div>
              <div className="shrink-0 text-right">
                <p className="text-xs text-gray-500 dark:text-gray-400">{n.createdBy}</p>
                <p className="text-xs text-gray-400 dark:text-gray-500 mt-0.5">{formatDate(n.createdAt)}</p>
              </div>
            </div>
          ))}
        </div>
      </section>

      {editOpen && (
        <CustomerModal
          customerId={id!}
          onClose={() => setEditOpen(false)}
          onSaved={loadCustomer}
        />
      )}
    </div>
  );
}
