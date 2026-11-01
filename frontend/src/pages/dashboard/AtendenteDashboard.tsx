import { useNavigate } from 'react-router-dom';
import { dashboardService } from '@/services/dashboardService';
import { cn, formatCurrency } from '@/lib/utils';
import { useDashboardData } from './useDashboardData';
import {
  DailySalesChart,
  DashboardFallback,
  DashboardHeader,
  Panel,
  StatTile,
} from './DashboardParts';
import { formatQuantity, salesCount } from './dashboardFormat';

export function AtendenteDashboard() {
  const navigate = useNavigate();
  const { data, loading, failed, retry } = useDashboardData(dashboardService.getSalesperson);

  const actions = (
    <>
      <button
        onClick={() => navigate('/quotations/new')}
        className="rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-brand-700"
      >
        Novo Orçamento
      </button>
      <button
        onClick={() => navigate('/quotations')}
        className="rounded-lg border border-gray-200 dark:border-brand-800/50 bg-white dark:bg-brand-900/20 px-4 py-2.5 text-sm font-medium text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-brand-800/30"
      >
        Ver Orçamentos
      </button>
    </>
  );

  return (
    <div className={cn('space-y-5 transition-opacity', loading && data && 'opacity-60')}>
      <DashboardHeader title="Minhas vendas" subtitle="Vendas concluídas em que você é o vendedor">
        {actions}
      </DashboardHeader>

      {!data ? (
        <DashboardFallback failed={failed} onRetry={retry} />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatTile label="Vendas hoje" value={formatCurrency(data.today.revenue)} hint={salesCount(data.today.count)} />
            <StatTile
              label="Vendas no mês"
              value={formatCurrency(data.month.revenue)}
              hint={salesCount(data.month.count)}
              comparison={{ current: data.month.revenue, previous: data.previousMonthToDate.revenue }}
            />
            <StatTile label="Ticket médio no mês" value={formatCurrency(data.month.averageTicket)} />
            <StatTile
              label="Orçamentos em aberto"
              value={formatCurrency(data.openQuotations.amount)}
              hint={`${formatQuantity(data.openQuotations.count)} em rascunho ou finalizados`}
            />
          </div>

          <Panel title="Minhas vendas por dia" subtitle="Faturamento neste mês">
            <DailySalesChart data={data.dailySales} />
          </Panel>
        </>
      )}
    </div>
  );
}
