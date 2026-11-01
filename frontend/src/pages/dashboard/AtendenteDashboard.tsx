import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/Button';
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
  const { data, loading, failed, retry } = useDashboardData(dashboardService.getSalesperson);

  const actions = (
    <>
      <Button asChild><Link to="/quotations/new">Novo orçamento</Link></Button>
      <Button asChild variant="outline"><Link to="/quotations">Ver orçamentos</Link></Button>
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
          <div className="grid grid-cols-2 gap-3 md:gap-4 lg:grid-cols-4">
            <StatTile lead label="Vendas hoje" value={formatCurrency(data.today.revenue)} hint={salesCount(data.today.count)} />
            <StatTile
              lead
              label="Vendas no mês"
              value={formatCurrency(data.month.revenue)}
              hint={salesCount(data.month.count)}
              comparison={{ current: data.month.revenue, previous: data.previousMonthToDate.revenue }}
            />
            <StatTile label="Ticket médio" value={formatCurrency(data.month.averageTicket)} />
            <StatTile
              label="Orçamentos em aberto"
              to="/quotations"
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
