import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Plus } from 'lucide-react';
import { quotationService } from '@/services/quotationService';
import type { Quotation, QuotationStatus } from '@/types/quotation';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { PageHeader } from '@/components/ui/PageHeader';
import { formatCurrency, formatDate } from '@/lib/utils';

const STATUS_LABELS: Record<QuotationStatus, string> = {
  draft: 'Rascunho',
  finalized: 'Finalizado',
  converted: 'Convertido',
  cancelled: 'Cancelado',
};

const COLUMNS: Column<Quotation>[] = [
  {
    key: 'number',
    header: 'Número',
    className: 'whitespace-nowrap',
    cell: q => (
      <Link to={`/quotations/${q.id}`} className="font-mono text-xs font-medium text-brand-700 hover:underline dark:text-brand-300">
        {q.quotationNumber}
      </Link>
    ),
  },
  { key: 'customer', header: 'Cliente', cell: q => q.customerName || '—' },
  { key: 'salesperson', header: 'Atendente', cell: q => q.salespersonName || '—' },
  { key: 'date', header: 'Data', className: 'whitespace-nowrap', cell: q => formatDate(q.createdAt) },
  { key: 'total', header: 'Total', align: 'right', className: 'whitespace-nowrap font-medium text-gray-900 dark:text-white', cell: q => formatCurrency(q.totalAmount) },
];

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
      <PageHeader title="Orçamentos">
        <Button asChild>
          <Link to="/quotations/new"><Plus size={16} />Novo orçamento</Link>
        </Button>
      </PageHeader>

      {/* A aba diz qual status é a lista inteira, então a linha não precisa repetir. */}
      <div role="tablist" aria-label="Situação do orçamento" className="-mx-4 flex gap-2 overflow-x-auto px-4 md:mx-0 md:px-0">
        {(['draft', 'finalized', 'converted', 'cancelled'] as QuotationStatus[]).map(s => (
          <button
            key={s}
            role="tab"
            aria-selected={status === s}
            onClick={() => setStatus(s)}
            className={`min-h-11 shrink-0 rounded-full px-4 text-sm font-medium transition-colors md:min-h-9 ${
              status === s
                ? 'bg-brand-600 text-white'
                : 'bg-gray-200/70 text-gray-700 hover:bg-gray-200 dark:bg-white/5 dark:text-gray-300 dark:hover:bg-white/10'
            }`}
          >
            {STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      <DataTable
        caption={`Orçamentos: ${STATUS_LABELS[status]}`}
        rows={quotations}
        columns={COLUMNS}
        rowKey={q => q.id}
        loading={loading}
        empty={`Nenhum orçamento ${STATUS_LABELS[status].toLowerCase()}.`}
        card={q => (
          <Link to={`/quotations/${q.id}`} className="-m-3 block rounded-lg p-3">
            <div className="flex items-baseline justify-between gap-3">
              <span className="min-w-0 font-medium text-gray-900 dark:text-white">{q.customerName || 'Sem cliente'}</span>
              <span className="shrink-0 font-semibold tabular-nums text-gray-900 dark:text-white">{formatCurrency(q.totalAmount)}</span>
            </div>
            <div className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
              <span className="font-mono">{q.quotationNumber}</span> · {formatDate(q.createdAt)}
              {q.salespersonName && ` · ${q.salespersonName}`}
            </div>
          </Link>
        )}
      />
    </div>
  );
}
