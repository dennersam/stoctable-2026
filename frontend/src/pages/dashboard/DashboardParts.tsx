import { useState, type ReactNode } from 'react';
import { ArrowDownRight, ArrowUpRight, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { cn, formatCurrency } from '@/lib/utils';
import type { DailySales } from '@/types/dashboard';
import { salesCount } from './dashboardFormat';

// Cor das marcas dos gráficos. brand-600 fica escuro demais para o gráfico no
// modo claro e brand-400 claro demais no escuro: estes dois passam na validação
// de contraste e luminosidade contra a superfície de cada modo.
const MARK_COLOR = 'bg-brand-500 dark:bg-[#d23adb]';

const compactNumber = new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 2 });

// A data vem como yyyy-MM-dd, o dia do calendário da loja. new Date() a leria
// como meia-noite UTC, que em São Paulo ainda é o dia anterior.
function dayLabel(date: string) {
  const [, month, day] = date.split('-');
  return `${day}/${month}`;
}

export function DashboardHeader({ title, subtitle, children }: { title: string; subtitle: string; children?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">{title}</h1>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{subtitle}</p>
      </div>
      {children && <div className="flex flex-wrap gap-2">{children}</div>}
    </div>
  );
}

export function DashboardFallback({ failed, onRetry }: { failed: boolean; onRetry: () => void }) {
  if (!failed) {
    return <p className="p-4 text-sm text-gray-500 dark:text-gray-400">Carregando painel…</p>;
  }
  return (
    <div className="p-4 text-sm text-gray-700 dark:text-gray-200">
      <p>Não foi possível carregar o painel.</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-2 rounded-md border border-gray-300 px-3 py-1.5 font-medium hover:bg-gray-50 dark:border-white/15 dark:hover:bg-white/10"
      >
        Tentar novamente
      </button>
    </div>
  );
}

/** Variação contra o mesmo tempo decorrido do mês anterior. */
function MonthDelta({ current, previous }: { current: number; previous: number }) {
  if (previous === 0) {
    return <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">Sem vendas no mesmo período do mês anterior</p>;
  }

  const change = ((current - previous) / previous) * 100;
  const up = change >= 0;
  const Icon = up ? ArrowUpRight : ArrowDownRight;
  const percent = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1, signDisplay: 'always' }).format(change);

  return (
    <p className="mt-1 flex items-start gap-1 text-xs text-gray-500 dark:text-gray-400">
      <Icon
        aria-hidden="true"
        className={cn('h-4 w-4 shrink-0', up ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400')}
      />
      <span>
        <span className="font-semibold text-gray-900 dark:text-white">{percent}%</span> sobre o mesmo período do mês anterior
      </span>
    </p>
  );
}

interface StatTileProps {
  label: string;
  value: string;
  hint?: string;
  comparison?: { current: number; previous: number };
}

export function StatTile({ label, value, hint, comparison }: StatTileProps) {
  return (
    <Card className="p-5">
      <p className="text-sm text-gray-500 dark:text-gray-400">{label}</p>
      <p className="mt-1 break-words text-2xl font-semibold text-gray-900 dark:text-white">{value}</p>
      {hint && <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{hint}</p>}
      {comparison && <MonthDelta {...comparison} />}
    </Card>
  );
}

export function Panel({ title, subtitle, action, children }: { title: string; subtitle?: string; action?: ReactNode; children: ReactNode }) {
  return (
    <Card className="min-w-0 p-5">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">{title}</h2>
          {subtitle && <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{subtitle}</p>}
        </div>
        {action}
      </div>
      {children}
    </Card>
  );
}

export function EmptyNote({ children }: { children: ReactNode }) {
  return <p className="py-4 text-sm text-gray-500 dark:text-gray-400">{children}</p>;
}

function niceCeiling(value: number) {
  if (value <= 0) return 1;
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const step = [1, 2, 2.5, 5, 10].find((s) => s * magnitude >= value) ?? 10;
  return step * magnitude;
}

/** Faturamento por dia do mês: uma série só, então o título já a identifica. */
export function DailySalesChart({ data }: { data: DailySales[] }) {
  const [hovered, setHovered] = useState<number | null>(null);

  if (data.every((d) => d.count === 0)) {
    return <EmptyNote>Nenhuma venda concluída neste mês.</EmptyNote>;
  }

  const ceiling = niceCeiling(Math.max(...data.map((d) => d.revenue)));
  const ticks = [1, 0.5, 0].map((f) => f * ceiling);
  const active = hovered === null ? null : data[hovered];

  return (
    <div>
      <div className="flex gap-2">
        <div className="relative h-48 w-12 shrink-0 text-right text-xs tabular-nums text-gray-500 dark:text-gray-400" aria-hidden="true">
          {ticks.map((tick, i) => (
            <span key={tick} className="absolute right-0 -translate-y-1/2" style={{ top: `${(i / (ticks.length - 1)) * 100}%` }}>
              {compactNumber.format(tick)}
            </span>
          ))}
        </div>

        <div className="relative min-w-0 flex-1">
          <div className="absolute inset-x-0 top-0 h-48" aria-hidden="true">
            {ticks.map((tick, i) => (
              <div
                key={tick}
                className="absolute inset-x-0 border-t border-gray-200 dark:border-white/10"
                style={{ top: `${(i / (ticks.length - 1)) * 100}%` }}
              />
            ))}
          </div>

          <div className="relative flex h-48 items-stretch" role="group" aria-label="Faturamento por dia">
            {data.map((day, i) => (
              <div
                key={day.date}
                tabIndex={0}
                role="img"
                aria-label={`${dayLabel(day.date)}: ${formatCurrency(day.revenue)}, ${salesCount(day.count)}`}
                className="flex min-w-0 flex-1 items-end justify-center px-px outline-none focus-visible:ring-2 focus-visible:ring-brand-500"
                onPointerEnter={() => setHovered(i)}
                onPointerLeave={() => setHovered(null)}
                onFocus={() => setHovered(i)}
                onBlur={() => setHovered(null)}
              >
                {day.revenue > 0 && (
                  <div
                    className={cn('w-full max-w-6 rounded-t transition-opacity', MARK_COLOR, hovered === i ? 'opacity-70' : 'opacity-100')}
                    style={{ height: `${(day.revenue / ceiling) * 100}%` }}
                  />
                )}
              </div>
            ))}
          </div>

          <div className="relative mt-1 flex text-xs tabular-nums text-gray-500 dark:text-gray-400" aria-hidden="true">
            {data.map((day, i) => (
              <span key={day.date} className="min-w-0 flex-1 overflow-visible whitespace-nowrap text-center">
                {i === 0 || (i + 1) % 5 === 0 ? day.date.slice(8) : ''}
              </span>
            ))}
          </div>

          {active && hovered !== null && (
            <div
              className="pointer-events-none absolute top-0 z-10 whitespace-nowrap rounded-md border border-gray-200 bg-white px-3 py-2 text-xs shadow-md dark:border-white/15 dark:bg-gray-900"
              style={hovered > data.length / 2
                ? { right: `${((data.length - hovered) / data.length) * 100}%` }
                : { left: `${((hovered + 1) / data.length) * 100}%` }}
            >
              <p className="font-semibold text-gray-900 dark:text-white">{formatCurrency(active.revenue)}</p>
              <p className="text-gray-500 dark:text-gray-400">{dayLabel(active.date)} · {salesCount(active.count)}</p>
            </div>
          )}
        </div>
      </div>

      <details className="mt-3 text-sm">
        <summary className="cursor-pointer text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200">
          Ver como tabela
        </summary>
        <div className="mt-2 max-h-56 overflow-y-auto">
          <table className="w-full text-left text-xs text-gray-700 dark:text-gray-200">
            <thead className="text-gray-500 dark:text-gray-400">
              <tr>
                <th className="py-1 font-medium">Dia</th>
                <th className="py-1 text-right font-medium">Vendas</th>
                <th className="py-1 text-right font-medium">Faturamento</th>
              </tr>
            </thead>
            <tbody className="tabular-nums">
              {data.map((day) => (
                <tr key={day.date} className="border-t border-gray-100 dark:border-white/5">
                  <td className="py-1">{dayLabel(day.date)}</td>
                  <td className="py-1 text-right">{day.count}</td>
                  <td className="py-1 text-right">{formatCurrency(day.revenue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </div>
  );
}

export interface RankedRow {
  key: string;
  label: string;
  detail?: string;
  value: number;
}

/** Lista ordenada com barra: o nome e o valor ficam legíveis sem passar o mouse. */
export function RankedBars({ rows, empty }: { rows: RankedRow[]; empty: string }) {
  if (rows.length === 0) return <EmptyNote>{empty}</EmptyNote>;

  const max = Math.max(...rows.map((r) => r.value), 1);

  return (
    <ul className="space-y-3">
      {rows.map((row) => (
        <li key={row.key}>
          <div className="flex items-baseline justify-between gap-3 text-sm">
            <span className="min-w-0 truncate text-gray-800 dark:text-gray-100" title={row.label}>{row.label}</span>
            <span className="shrink-0 font-medium tabular-nums text-gray-900 dark:text-white">{formatCurrency(row.value)}</span>
          </div>
          {row.detail && <p className="text-xs text-gray-500 dark:text-gray-400">{row.detail}</p>}
          <div className="mt-1 h-2">
            <div className={cn('h-full rounded-r', MARK_COLOR)} style={{ width: `${(row.value / max) * 100}%` }} />
          </div>
        </li>
      ))}
    </ul>
  );
}

const TONES = {
  neutral: 'text-gray-500 dark:text-gray-400',
  warning: 'text-amber-700 dark:text-amber-400',
  critical: 'text-red-700 dark:text-red-400',
};

export function StockFigure({ icon: Icon, label, value, detail, tone = 'neutral' }: {
  icon: LucideIcon;
  label: string;
  value: string;
  detail?: string;
  tone?: keyof typeof TONES;
}) {
  return (
    <div className="min-w-0">
      <p className="flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
        <Icon aria-hidden="true" className={cn('h-4 w-4 shrink-0', TONES[tone])} />
        {label}
      </p>
      <p className="mt-1 text-xl font-semibold text-gray-900 dark:text-white">{value}</p>
      {detail && <p className="text-xs text-gray-500 dark:text-gray-400">{detail}</p>}
    </div>
  );
}
