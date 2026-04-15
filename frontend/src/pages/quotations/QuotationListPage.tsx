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
  draft: 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300',
  finalized: 'bg-brand-100 dark:bg-brand-900/40 text-brand-700 dark:text-brand-400',
  converted: 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400',
  cancelled: 'bg-red-100 dark:bg-red-900/40 text-red-500 dark:text-red-400',
};

export function QuotationListPage() {
  const [quotations, setQuotations] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<QuotationStatus>('draft');

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const data = await quotationService.getAll({ status });
      setQuotations(data);
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
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Orçamentos</h1>
        <Link
          to="/quotations/new"
          className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
        >
          Novo orçamento
        </Link>
      </div>

      <div className="flex flex-wrap gap-2">
        {(['draft', 'finalized', 'converted', 'cancelled'] as QuotationStatus[]).map(s => (
          <button
            key={s}
            onClick={() => setStatus(s)}
            className={`rounded-full px-3 py-1 text-sm font-medium transition-colors ${
              status === s
                ? 'bg-brand-600 text-white'
                : 'bg-gray-100 dark:bg-brand-900/30 text-gray-600 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-brand-800/40'
            }`}
          >
            {STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-brand-800/50">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-brand-800/40">
            <thead className="bg-gray-50 dark:bg-brand-900/40">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Número</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Cliente</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">Atendente</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Total</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500 dark:text-brand-300/70 hidden sm:table-cell">Data</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-brand-800/40 bg-white dark:bg-brand-950">
              {quotations.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-8 text-center text-gray-400 dark:text-gray-500">
                    Nenhum orçamento {STATUS_LABELS[status].toLowerCase()}.
                  </td>
                </tr>
              ) : (
                quotations.map(q => (
                  <tr key={q.id} className="hover:bg-gray-50 dark:hover:bg-brand-800/20 transition-colors">
                    <td className="px-4 py-3 font-mono text-sm text-gray-700 dark:text-gray-300">{q.quotationNumber}</td>
                    <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">{q.customerName || '—'}</td>
                    <td className="px-4 py-3 text-sm text-gray-500 dark:text-gray-400 hidden sm:table-cell">{q.salespersonName || '—'}</td>
                    <td className="px-4 py-3 text-right text-sm font-medium text-gray-900 dark:text-white">
                      R$ {q.totalAmount.toFixed(2)}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[q.status]}`}>
                        {STATUS_LABELS[q.status]}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-400 dark:text-gray-500 hidden sm:table-cell">
                      {new Date(q.createdAt).toLocaleDateString('pt-BR')}
                    </td>
                    <td className="px-4 py-3 text-right text-sm">
                      <Link to={`/quotations/${q.id}`} className="text-brand-600 dark:text-brand-400 hover:underline">
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
