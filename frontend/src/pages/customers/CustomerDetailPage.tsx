import { useState, useEffect, useCallback } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { customerService } from '@/services/customerService';
import type { Customer, CustomerCrmNote } from '@/types/customer';
import { CustomerModal } from './CustomerListPage';
import { formatCurrency } from '@/lib/utils';
import { confirmDialog } from '@/lib/dialogs';
import { ArrowLeft } from 'lucide-react';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { fieldClass } from '@/components/ui/fieldClass';

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
    <div className="min-w-0">
      <p className="text-xs text-gray-500 dark:text-gray-400">{label}</p>
      <p className="text-sm font-medium text-gray-900 dark:text-white mt-0.5 break-words">
        {value || <span className="text-gray-500 dark:text-gray-400 font-normal">—</span>}
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
    if (!(await confirmDialog({ title: 'Desativar cliente', description: `"${customer.fullName}" ficará inativo.`, confirmLabel: 'Desativar cliente' }))) return;
    try {
      await customerService.update(id!, { ...customer, isActive: false } as any);
      toast.success('Cliente desativado.');
      navigate('/customers');
    } catch {
      toast.error('Erro ao desativar cliente.');
    }
  }

  if (loading) {
    return <div role="status" className="py-16 text-center text-sm text-gray-500 dark:text-gray-400">Carregando...</div>;
  }
  if (!customer) return null;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      {/* Header */}
      <div>
        <Link
          to="/customers"
          className="-ml-1 inline-flex min-h-11 items-center gap-1 text-sm text-gray-600 hover:text-gray-900 md:min-h-0 dark:text-gray-300 dark:hover:text-white"
        >
          <ArrowLeft size={16} />
          Clientes
        </Link>
        <div className="mt-1 flex flex-wrap items-center justify-between gap-3">
          <h1 className="min-w-0 text-2xl font-bold text-gray-900 dark:text-white">
            {customer.fullName}
            {!customer.isActive && <Badge variant="neutral" className="ml-2 align-middle">Inativo</Badge>}
          </h1>
          <div className="flex gap-2 max-sm:w-full max-sm:[&>*]:flex-1">
            {customer.isActive && (
              <Button variant="danger-outline" onClick={handleDeactivate}>Desativar</Button>
            )}
            <Button variant="outline" onClick={() => setEditOpen(true)}>Editar</Button>
          </div>
        </div>
      </div>

      {/* ── Dados ─────────────────────────────────────────────────────── */}
      <section className="rounded-lg border border-line bg-surface p-4 space-y-5 md:p-5">
        <h2 className="font-semibold text-gray-900 dark:text-white">
          Dados do cliente
        </h2>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <InfoRow label="Documento" value={customer.documentNumber ? `${customer.documentType}: ${customer.documentNumber}` : undefined} />
          <InfoRow label="E-mail" value={customer.email} />
          <InfoRow label="Celular / WhatsApp" value={customer.mobile} />
          <InfoRow label="Telefone" value={customer.phone} />
          <InfoRow label="CEP" value={customer.zipCode} />
          <InfoRow label="Limite de crédito" value={customer.creditLimit > 0 ? `${formatCurrency(customer.creditLimit)}` : undefined} />
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
      <section className="rounded-lg border border-line bg-surface p-4 space-y-4 md:p-5">
        <h2 className="font-semibold text-gray-900 dark:text-white">
          Histórico do cliente
        </h2>

        {/* Add note form */}
        <form onSubmit={handleAddNote} className="space-y-3">
          <textarea
            value={newNote}
            onChange={e => setNewNote(e.target.value)}
            rows={2}
            placeholder="Adicionar anotação sobre o cliente..."
            aria-label="Nova anotação"
            className={fieldClass + ' resize-none'}
          />
          <div className="flex flex-wrap items-center gap-2">
            <select
              aria-label="Tipo de anotação"
              value={newNoteType}
              onChange={e => setNewNoteType(e.target.value as CustomerCrmNote['noteType'])}
              className={fieldClass + ' min-w-0 flex-1 sm:w-auto sm:flex-none'}
            >
              <option value="general">Geral</option>
              <option value="followup">Acompanhamento</option>
              <option value="complaint">Reclamação</option>
            </select>
            <Button type="submit" disabled={addingNote || !newNote.trim()}>
              {addingNote ? 'Salvando...' : 'Adicionar nota'}
            </Button>
          </div>
        </form>

        {/* Notes list */}
        <div className="space-y-2 pt-2">
          {notesLoading ? (
            <p className="text-sm text-gray-500 dark:text-gray-400 text-center py-4">Carregando notas...</p>
          ) : notes.length === 0 ? (
            <p className="text-sm text-gray-500 dark:text-gray-400 text-center py-4">
              Nenhuma anotação registrada.
            </p>
          ) : notes.map(n => (
            <div
              key={n.id}
              className="rounded-lg border border-gray-100 dark:border-white/10 bg-gray-50 dark:bg-white/5 px-4 py-3 flex flex-col gap-2 sm:flex-row sm:gap-3"
            >
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1">
                  <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${NOTE_TYPE_COLOR[n.noteType as CustomerCrmNote['noteType']] ?? NOTE_TYPE_COLOR.general}`}>
                    {NOTE_TYPE_LABEL[n.noteType as CustomerCrmNote['noteType']] ?? n.noteType}
                  </span>
                </div>
                <p className="text-sm text-gray-800 dark:text-gray-200 whitespace-pre-wrap">{n.note}</p>
              </div>
              <div className="shrink-0 sm:text-right">
                <p className="text-xs text-gray-500 dark:text-gray-400">{n.createdBy}</p>
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{formatDate(n.createdAt)}</p>
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
