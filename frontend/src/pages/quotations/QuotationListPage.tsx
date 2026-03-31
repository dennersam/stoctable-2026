import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { quotationService } from '@/services/quotationService';
import type { Quotation, QuotationStatus } from '@/types/quotation';

const STATUS_LABELS: Record<QuotationStatus, string> = {
  draft: 'Rascunho',
  finalized: 'Finalizado',
  converted: 'Convertido',
  cancelled: 'Cancelado',
};

const STATUS_COLORS: Record<QuotationStatus, string> = {
  draft: 'bg-gray-100 text-gray-600',
  finalized: 'bg-blue-100 text-blue-700',
  converted: 'bg-green-100 text-green-700',
  cancelled: 'bg-red-100 text-red-500',
};

export function QuotationListPage() {
  const [quotations, setQuotations] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<QuotationStatus>('draft');

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const data = await quotationService.getAll({ status });
      setQuotations(Array.isArray(data) ? data : (data as any).items ?? []);
    } catch {
      toast.error('Erro ao carregar orçamentos.');
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Orçamentos</h1>
        <Link
          to="/quotations/new"
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Novo orçamento
        </Link>
      </div>

      <div className="flex gap-2">
        {(['draft', 'finalized', 'converted', 'cancelled'] as QuotationStatus[]).map(s => (
          <button
            key={s}
            onClick={() => setStatus(s)}
            className={`rounded-full px-3 py-1 text-sm font-medium transition-colors ${
              status === s
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            }`}
          >
            {STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="py-12 text-center text-gray-500">Carregando...</div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-gray-200">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Número</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Cliente</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Atendente</th>
                <th className="px-4 py-3 text-right text-xs font-medium uppercase text-gray-500">Total</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Data</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {quotations.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-8 text-center text-gray-400">
                    Nenhum orçamento {STATUS_LABELS[status].toLowerCase()}.
                  </td>
                </tr>
              ) : (
                quotations.map(q => (
                  <tr key={q.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-mono text-sm text-gray-700">{q.quotationNumber}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{q.customerName || '—'}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{q.salespersonName || '—'}</td>
                    <td className="px-4 py-3 text-right text-sm font-medium text-gray-900">
                      R$ {q.totalAmount.toFixed(2)}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[q.status]}`}>
                        {STATUS_LABELS[q.status]}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-400">
                      {new Date(q.createdAt).toLocaleDateString('pt-BR')}
                    </td>
                    <td className="px-4 py-3 text-right text-sm">
                      <Link to={`/quotations/${q.id}`} className="text-blue-600 hover:underline">
                        Ver
                      </Link>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
