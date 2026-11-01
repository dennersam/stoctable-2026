import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { ArrowLeft } from 'lucide-react';
import { quotationService } from '@/services/quotationService';
import { useAuthStore } from '@/store/authStore';
import type { Quotation } from '@/types/quotation';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field } from '@/components/ui/Field';
import { fieldClass } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { confirmDialog } from '@/lib/dialogs';
import { formatCurrency, formatDateTime } from '@/lib/utils';

const STATUS_LABELS: Record<string, string> = {
  draft: 'Rascunho',
  finalized: 'Finalizado',
  converted: 'Convertido',
  cancelled: 'Cancelado',
};

const STATUS_COLORS: Record<string, string> = {
  draft: 'bg-gray-100 dark:bg-white/10 text-gray-700 dark:text-gray-300',
  finalized: 'bg-brand-100 dark:bg-brand-900/40 text-brand-700 dark:text-brand-300',
  converted: 'bg-green-100 dark:bg-green-900/40 text-green-800 dark:text-green-400',
  cancelled: 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-400',
};

type Item = Quotation['items'][number];

const COLUMNS: Column<Item>[] = [
  {
    key: 'product',
    header: 'Produto',
    cell: item => (
      <>
        <div className="font-medium text-gray-900 dark:text-white">{item.productName}</div>
        <div className="font-mono text-xs text-gray-500 dark:text-gray-400">{item.productSku}</div>
      </>
    ),
  },
  { key: 'quantity', header: 'Qtd', align: 'right', cell: item => item.quantity },
  { key: 'unit', header: 'Unitário', align: 'right', className: 'whitespace-nowrap', cell: item => formatCurrency(item.unitPrice) },
  { key: 'discount', header: 'Desc.', align: 'right', cell: item => (item.discountPct > 0 ? `${item.discountPct}%` : '—') },
  { key: 'total', header: 'Total', align: 'right', className: 'whitespace-nowrap font-medium text-gray-900 dark:text-white', cell: item => formatCurrency(item.lineTotal) },
];

function Info({ label, children, wide = false }: { label: string; children: React.ReactNode; wide?: boolean }) {
  return (
    <div className={wide ? 'col-span-2' : undefined}>
      <dt className="text-gray-500 dark:text-gray-400">{label}</dt>
      <dd className="font-medium text-gray-900 dark:text-white">{children}</dd>
    </div>
  );
}

export function QuotationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuthStore();

  const [quotation, setQuotation] = useState<Quotation | null>(null);
  const [loading, setLoading] = useState(true);
  const [converting, setConverting] = useState(false);
  const [showCancel, setShowCancel] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  useEffect(() => {
    if (!id) return;
    quotationService.getById(id)
      .then(setQuotation)
      .catch(() => { toast.error('Orçamento não encontrado.'); navigate('/quotations'); })
      .finally(() => setLoading(false));
  }, [id, navigate]);

  const handleConvert = async () => {
    if (!quotation) return;
    const confirmed = await confirmDialog({
      title: 'Converter em venda',
      description: `O orçamento ${quotation.quotationNumber} vai para o caixa como venda aguardando pagamento.`,
      confirmLabel: 'Converter em venda',
    });
    if (!confirmed) return;
    setConverting(true);
    try {
      const { saleId } = await quotationService.convertToSale(quotation.id);
      toast.success('Orçamento convertido em venda! Redirecionando para o caixa...');
      navigate(`/checkout/${saleId}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.detail ?? 'Erro ao converter orçamento.');
    } finally {
      setConverting(false);
    }
  };

  const handleCancel = async () => {
    if (!quotation || !cancelReason.trim()) {
      toast.error('Informe o motivo do cancelamento.');
      return;
    }
    setCancelling(true);
    try {
      await quotationService.cancel(quotation.id, { cancellationReason: cancelReason });
      toast.success('Orçamento cancelado.');
      navigate('/quotations');
    } catch {
      toast.error('Erro ao cancelar orçamento.');
    } finally {
      setCancelling(false);
    }
  };

  if (loading) return <div role="status" className="flex h-64 items-center justify-center text-sm text-gray-500 dark:text-gray-400">Carregando...</div>;
  if (!quotation) return null;

  const canConvert = quotation.status === 'finalized' && (user?.role === 'admin' || user?.role === 'caixa');
  const canCancel = quotation.status === 'draft' || quotation.status === 'finalized';

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      {/* Header */}
      <div>
        <Link
          to="/quotations"
          className="-ml-1 inline-flex min-h-11 items-center gap-1 text-sm text-gray-600 hover:text-gray-900 md:min-h-0 dark:text-gray-300 dark:hover:text-white"
        >
          <ArrowLeft size={16} />
          Orçamentos
        </Link>
        <div className="mt-1 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="whitespace-nowrap text-xl font-bold text-gray-900 dark:text-white">{quotation.quotationNumber}</h1>
            <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[quotation.status]}`}>
              {STATUS_LABELS[quotation.status]}
            </span>
          </div>
          {(canConvert || canCancel) && (
            <div className="flex flex-wrap gap-2 max-sm:w-full max-sm:[&>*]:flex-1">
              {canCancel && (
                <Button variant="danger-outline" onClick={() => setShowCancel(true)}>Cancelar orçamento</Button>
              )}
              {canConvert && (
                <Button variant="success" onClick={handleConvert} disabled={converting}>
                  {converting ? 'Convertendo...' : 'Converter em venda'}
                </Button>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Info */}
      <dl className="grid grid-cols-2 gap-4 rounded-lg border border-line bg-surface p-4 text-sm md:p-5">
        <Info label="Cliente">{quotation.customerName || '—'}</Info>
        <Info label="Atendente">{quotation.salespersonName || '—'}</Info>
        <Info label="Criado em">{formatDateTime(quotation.createdAt)}</Info>
        {quotation.validUntil && (
          <Info label="Válido até">{new Date(quotation.validUntil).toLocaleDateString('pt-BR')}</Info>
        )}
        {quotation.notes && <Info label="Observações" wide>{quotation.notes}</Info>}
        {quotation.cancellationReason && (
          <Info label="Motivo do cancelamento" wide>
            <span className="text-red-700 dark:text-red-400">{quotation.cancellationReason}</span>
          </Info>
        )}
      </dl>

      {/* Items */}
      <DataTable
        caption="Itens do orçamento"
        rows={quotation.items}
        columns={COLUMNS}
        rowKey={item => item.id}
        empty="Este orçamento não tem itens."
        card={item => (
          <>
            <div className="font-medium text-gray-900 dark:text-white">{item.productName}</div>
            <div className="font-mono text-xs text-gray-500 dark:text-gray-400">{item.productSku}</div>
            <div className="mt-2 flex items-baseline justify-between gap-3 text-sm tabular-nums">
              <span className="text-gray-600 dark:text-gray-300">
                {item.quantity} × {formatCurrency(item.unitPrice)}
                {item.discountPct > 0 && ` (-${item.discountPct}%)`}
              </span>
              <span className="font-semibold text-gray-900 dark:text-white">{formatCurrency(item.lineTotal)}</span>
            </div>
          </>
        )}
      />

      {/* Totals */}
      <dl className="space-y-2 rounded-lg border border-line bg-surface p-4 text-sm tabular-nums">
        <div className="flex justify-between text-gray-600 dark:text-gray-300">
          <dt>Subtotal</dt>
          <dd>{formatCurrency(quotation.subtotal)}</dd>
        </div>
        {quotation.discountAmount > 0 && (
          <div className="flex justify-between text-gray-600 dark:text-gray-300">
            <dt>Desconto ({quotation.discountPct}%)</dt>
            <dd>- {formatCurrency(quotation.discountAmount)}</dd>
          </div>
        )}
        <div className="flex items-baseline justify-between border-t border-line pt-2 text-gray-900 dark:text-white">
          <dt className="font-medium">Total</dt>
          <dd className="text-xl font-bold">{formatCurrency(quotation.totalAmount)}</dd>
        </div>
      </dl>

      {/* Cancel dialog */}
      {showCancel && (
        <Modal title="Cancelar orçamento" onClose={() => setShowCancel(false)}>
          <ModalBody>
            <Field label="Motivo" required>
              <textarea
                autoFocus
                value={cancelReason}
                onChange={e => setCancelReason(e.target.value)}
                rows={3}
                className={`${fieldClass} resize-none`}
                placeholder="Informe o motivo do cancelamento"
              />
            </Field>
          </ModalBody>
          <ModalFooter>
            <Button variant="outline" onClick={() => setShowCancel(false)}>Voltar</Button>
            <Button variant="danger" onClick={handleCancel} disabled={cancelling || !cancelReason.trim()}>
              {cancelling ? 'Cancelando...' : 'Cancelar orçamento'}
            </Button>
          </ModalFooter>
        </Modal>
      )}
    </div>
  );
}
