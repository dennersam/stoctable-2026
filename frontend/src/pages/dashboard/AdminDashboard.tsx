import { Link } from 'react-router-dom';
import { AlertTriangle, Archive, PackageX } from 'lucide-react';
import { dashboardService } from '@/services/dashboardService';
import { useBranchStore } from '@/store/branchStore';
import { cn, formatCurrency } from '@/lib/utils';
import { useDashboardData } from './useDashboardData';
import {
  DailySalesChart,
  DashboardFallback,
  DashboardHeader,
  EmptyNote,
  Panel,
  RankedBars,
  StatTile,
  StockFigure,
} from './DashboardParts';
import { formatQuantity, paymentsCount, salesCount } from './dashboardFormat';

export function AdminDashboard() {
  const { data, loading, failed, retry } = useDashboardData(dashboardService.getAdmin);
  const branch = useBranchStore((s) => s.active());

  if (!data) return <DashboardFallback failed={failed} onRetry={retry} />;

  const { today, month, previousMonthToDate, margin, stock } = data;

  return (
    <div className={cn('space-y-5 transition-opacity', loading && 'opacity-60')}>
      <DashboardHeader
        title="Visão geral"
        subtitle={branch ? `Números da filial ${branch.name}` : 'Números da filial ativa'}
      />

      <div className="grid grid-cols-2 gap-3 md:gap-4 lg:grid-cols-4">
        <StatTile lead label="Vendas hoje" value={formatCurrency(today.revenue)} hint={salesCount(today.count)} />
        <StatTile
          lead
          label="Vendas no mês"
          value={formatCurrency(month.revenue)}
          comparison={{ current: month.revenue, previous: previousMonthToDate.revenue }}
        />
        <StatTile
          label="Ticket médio"
          value={formatCurrency(month.averageTicket)}
          comparison={{ current: month.averageTicket, previous: previousMonthToDate.averageTicket }}
        />
        <StatTile
          label="Margem estimada"
          value={`${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(margin.percent)}%`}
          hint={`${formatCurrency(margin.profit)} de lucro pelo custo atual`}
        />
        <StatTile
          label="Orçamentos em aberto"
          to="/quotations"
          value={formatCurrency(data.openQuotations.amount)}
          hint={`${formatQuantity(data.openQuotations.count)} em rascunho ou finalizados`}
        />
        <StatTile
          label="Aguardando pagamento"
          to="/checkout"
          value={formatCurrency(data.pendingSales.amountRemaining)}
          hint={salesCount(data.pendingSales.count)}
        />
      </div>

      <Panel title="Faturamento por dia" subtitle="Vendas concluídas neste mês">
        <DailySalesChart data={data.dailySales} />
      </Panel>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Panel title="Produtos mais vendidos" subtitle="Por faturamento no mês">
          <RankedBars
            empty="Nenhum produto vendido neste mês."
            rows={data.topProducts.map((p) => ({
              key: p.productId,
              label: p.name,
              detail: `SKU ${p.sku} · ${formatQuantity(p.quantity)} un.`,
              value: p.revenue,
            }))}
          />
        </Panel>

        <div className="space-y-4">
          <Panel title="Vendedores" subtitle="Por faturamento no mês">
            <RankedBars
              empty="Nenhuma venda concluída neste mês."
              rows={data.topSalespeople.map((s, i) => ({
                key: `${s.name}-${i}`,
                label: s.name,
                detail: salesCount(s.count),
                value: s.revenue,
              }))}
            />
          </Panel>

          <Panel title="Recebimentos por forma de pagamento" subtitle="No mês">
            <RankedBars
              empty="Nenhum recebimento neste mês."
              rows={data.byMethod.map((m) => ({
                key: m.method,
                label: m.method,
                detail: paymentsCount(m.count),
                value: m.amount,
              }))}
            />
          </Panel>
        </div>
      </div>

      <Panel
        title="Saúde do estoque"
        action={
          <Link to="/inventory" className="inline-flex min-h-11 items-center text-sm font-medium text-brand-700 hover:underline md:min-h-0 dark:text-brand-300">
            Ver estoque
          </Link>
        }
      >
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <StockFigure icon={AlertTriangle} tone="warning" label="No mínimo ou abaixo" value={formatQuantity(stock.lowCount)} detail="produtos a repor" />
          <StockFigure icon={PackageX} tone="critical" label="Zerados" value={formatQuantity(stock.outCount)} detail="com mínimo definido" />
          <StockFigure
            icon={Archive}
            label="Parados há 180 dias"
            value={formatQuantity(stock.deadCount)}
            detail={`${formatCurrency(stock.deadValue)} a preço de custo`}
          />
        </div>

        <div className="mt-5 border-t border-gray-100 pt-4 dark:border-white/10">
          <h3 className="mb-2 text-sm font-medium text-gray-800 dark:text-gray-100">Mais urgentes para repor</h3>
          {stock.lowItems.length === 0 ? (
            <EmptyNote>Nenhum produto no mínimo ou abaixo.</EmptyNote>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm text-gray-700 dark:text-gray-200">
                <thead className="text-xs text-gray-500 dark:text-gray-400">
                  <tr>
                    <th className="py-1.5 pr-3 font-medium">Produto</th>
                    <th className="py-1.5 pr-3 font-medium">SKU</th>
                    <th className="py-1.5 pr-3 text-right font-medium">Saldo</th>
                    <th className="py-1.5 text-right font-medium">Mínimo</th>
                  </tr>
                </thead>
                <tbody className="tabular-nums">
                  {stock.lowItems.map((item) => (
                    <tr key={item.productId} className="border-t border-gray-100 dark:border-white/5">
                      <td className="py-1.5 pr-3">{item.name}</td>
                      <td className="py-1.5 pr-3 text-gray-500 dark:text-gray-400">{item.sku}</td>
                      <td className="py-1.5 pr-3 text-right">{formatQuantity(item.quantity)}</td>
                      <td className="py-1.5 text-right">{formatQuantity(item.minimum)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </Panel>
    </div>
  );
}
