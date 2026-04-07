import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { quotationService } from '@/services/quotationService';
import { useAuthStore } from '@/store/authStore';
import type { Quotation } from '@/types/quotation';

const STATUS_LABELS: Record<string, string> = {
  draft: 'Rascunho',
  finalized: 'Finalizado',
  converted: 'Convertido',
  cancelled: 'Cancelado',
};

const STATUS_COLORS: Record<string, string> = {
  draft: 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300',
  finalized: 'bg-brand-100 dark:bg-brand-900/40 text-brand-700 dark:text-brand-400',
  converted: 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400',
  cancelled: 'bg-red-100 dark:bg-red-900/40 text-red-500 dark:text-red-400',
};

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
    if (!confirm(`Converter orçamento ${quotation.quotationNumber} em venda?`)) return;
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

  if (loading) return <div className="flex h-64 items-center justify-center text-gray-500 dark:text-gray-400">Carregando...</div>;
  if (!quotation) return null;

  const canConvert = quotation.status === 'finalized' && (user?.role === 'admin' || user?.role === 'caixa');
  const canCancel = quotation.status === 'draft' || quotation.status === 'finalized';

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link to="/quotations" className="text-sm text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200">
            ← Orçamentos
          </Link>
          <span className="text-gray-300 dark:text-gray-600">/</span>
          <h1 className="text-xl font-bold text-gray-900 dark:text-white">{quotation.quotationNumber}</h1>
          <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[quotation.status]}`}>
            {STATUS_LABELS[quotation.status]}
          </span>
        </div>
        <div className="flex gap-2">
          {canConvert && (
            <button
              onClick={handleConvert}
              disabled={converting}
              className="rounded-md bg-green-600 hover:bg-green-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
            >
              {converting ? 'Convertendo...' : 'Converter em venda'}
            </button>
          )}
          {canCancel && (
            <button
              onClick={() => setShowCancel(true)}
              className="rounded-md border border-red-300 dark:border-red-700 px-4 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20"
            >
              Cancelar
            </button>
          )}
        </div>
      </div>

      {/* Info */}
      <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 grid grid-cols-2 gap-4 text-sm">
        <div>
          <span className="text-gray-500 dark:text-gray-400">Cliente</span>
          <p className="font-medium text-gray-900 dark:text-white">{quotation.customerName || '—'}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Atendente</span>
          <p className="font-medium text-gray-900 dark:text-white">{quotation.salespersonName || '—'}</p>
        </div>
        <div>
          <span className="text-gray-500 dark:text-gray-400">Criado em</span>
          <p className="font-medium text-gray-900 dark:text-white">
            {new Date(quotation.createdAt).toLocaleDateString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
          </p>
        </div>
        {quotation.validUntil && (
          <div>
            <span className="text-gray-500 dark:text-gray-400">Válido até</span>
            <p className="font-medium text-gray-900 dark:text-white">
              {new Date(quotation.validUntil).toLocaleDateString('pt-BR')}
            </p>
          </div>
        )}
        {quotation.notes && (
          <div className="col-span-2">
            <span className="text-gray-500 dark:text-gray-400">Observações</span>
            <p className="text-gray-900 dark:text-white">{quotation.notes}</p>
          </div>
        )}
        {quotation.cancellationReason && (
          <div className="col-span-2">
            <span className="text-gray-500 dark:text-gray-400">Motivo cancelamento</span>
            <p className="text-red-600 dark:text-red-400">{quotation.cancellationReason}</p>
          </div>
        )}
      </div>

      {/* Items */}
      <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
        <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead className="bg-gray-50 dark:bg-gray-800">
            <tr>
              {['Produto', 'SKU', 'Qtd', 'Preço unit.', 'Desc.', 'Total'].map(h => (
                <th key={h} className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-gray-700 bg-white dark:bg-gray-900">
            {quotation.items.map(item => (
              <tr key={item.id}>
                <td className="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">{item.productName}</td>
                <td className="px-4 py-3 text-xs font-mono text-gray-500 dark:text-gray-400">{item.productSku}</td>
                <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">{item.quantity}</td>
                <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">R$ {item.unitPrice.toFixed(2)}</td>
                <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">
                  {item.discountPct > 0 ? `${item.discountPct}%` : '—'}
                </td>
                <td className="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">R$ {item.lineTotal.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Totals */}
      <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 space-y-2 text-sm">
        <div className="flex justify-between text-gray-600 dark:text-gray-300">
          <span>Subtotal</span>
          <span>R$ {quotation.subtotal.toFixed(2)}</span>
        </div>
        {quotation.discountAmount > 0 && (
          <div className="flex justify-between text-red-600 dark:text-red-400">
            <span>Desconto ({quotation.discountPct}%)</span>
            <span>- R$ {quotation.discountAmount.toFixed(2)}</span>
          </div>
        )}
        <div className="flex justify-between border-t border-gray-100 dark:border-gray-700 pt-2 font-bold text-base">
          <span className="text-gray-900 dark:text-white">Total</span>
          <span className="text-brand-700 dark:text-brand-400">R$ {quotation.totalAmount.toFixed(2)}</span>
        </div>
      </div>

      {/* Cancel dialog */}
      {showCancel && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white dark:bg-gray-900 p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-semibold text-red-700 dark:text-red-400">Cancelar orçamento</h3>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Motivo *</label>
              <textarea
                value={cancelReason}
                onChange={e => setCancelReason(e.target.value)}
                rows={3}
                className="w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white px-3 py-2 text-sm"
                placeholder="Informe o motivo do cancelamento..."
              />
            </div>
            <div className="flex gap-2 justify-end">
              <button onClick={() => setShowCancel(false)} className="rounded border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800">
                Voltar
              </button>
              <button
                onClick={handleCancel}
                disabled={cancelling || !cancelReason.trim()}
                className="rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                {cancelling ? 'Cancelando...' : 'Confirmar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
