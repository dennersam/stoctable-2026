import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/Button';
import { dashboardService } from '@/services/dashboardService';
import { cn, formatCurrency, formatDateTime } from '@/lib/utils';
import { useDashboardData } from './useDashboardData';
import {
  DashboardFallback,
  DashboardHeader,
  EmptyNote,
  Panel,
  RankedBars,
  StatTile,
} from './DashboardParts';
import { formatQuantity, paymentsCount, salesCount } from './dashboardFormat';

export function CaixaDashboard() {
  const { data, loading, failed, retry } = useDashboardData(dashboardService.getCashier);

  return (
    <div className={cn('space-y-5 transition-opacity', loading && data && 'opacity-60')}>
      <DashboardHeader title="Caixa" subtitle="Movimento de hoje nesta filial">
        <Button asChild variant="success"><Link to="/checkout">Receber pagamentos</Link></Button>
      </DashboardHeader>

      {!data ? (
        <DashboardFallback failed={failed} onRetry={retry} />
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 md:gap-4 lg:grid-cols-4">
            <StatTile
              lead
              label="Recebido hoje"
              value={formatCurrency(data.receivedToday)}
              hint={paymentsCount(data.paymentsToday)}
            />
            <StatTile
              label="Aguardando pagamento"
              to="/checkout"
              value={formatCurrency(data.pendingSales.amountRemaining)}
              hint={salesCount(data.pendingSales.count)}
            />
            <StatTile
              label="Estornos hoje"
              value={formatCurrency(data.refundsToday.amount)}
              hint={`${formatQuantity(data.refundsToday.count)} ${data.refundsToday.count === 1 ? 'estornado' : 'estornados'}`}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <Panel title="Recebido por forma de pagamento" subtitle="Hoje">
              <RankedBars
                empty="Nenhum recebimento hoje."
                rows={data.byMethod.map((m) => ({
                  key: m.method,
                  label: m.method,
                  detail: paymentsCount(m.count),
                  value: m.amount,
                }))}
              />
            </Panel>

            <Panel title="Fila de pagamento" subtitle="Vendas esperando há mais tempo">
              {data.pendingSales.oldest.length === 0 ? (
                <EmptyNote>Nenhuma venda aguardando pagamento.</EmptyNote>
              ) : (
                <ul className="divide-y divide-gray-100 dark:divide-white/5">
                  {data.pendingSales.oldest.map((sale) => (
                    <li key={sale.saleId}>
                      <Link
                        to={`/checkout/${sale.saleId}`}
                        className="flex items-center justify-between gap-3 rounded-md px-1 py-2.5 hover:bg-gray-50 dark:hover:bg-white/5"
                      >
                        <span className="min-w-0">
                          <span className="block truncate text-sm font-medium text-gray-900 dark:text-white">
                            {sale.customerName ?? 'Cliente não identificado'}
                          </span>
                          <span className="block text-xs text-gray-500 dark:text-gray-400">
                            {sale.saleNumber} · {formatDateTime(sale.createdAt)}
                          </span>
                        </span>
                        <span className="shrink-0 text-sm font-medium tabular-nums text-gray-900 dark:text-white">
                          {formatCurrency(sale.amountRemaining)}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </Panel>
          </div>
        </>
      )}
    </div>
  );
}
