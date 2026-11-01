import { Banknote, Boxes, FileText, type LucideIcon } from 'lucide-react';
import { formatCurrency } from '@/lib/utils';

const ITEMS = [
  { sku: 'PN-90/90-18', name: 'Pneu traseiro Pirelli', quantity: 1, price: 389.9 },
  { sku: 'VL-CR7HSA', name: 'Vela NGK CR7HSA', quantity: 2, price: 32.5 },
  { sku: 'OL-10W30', name: 'Óleo 10W30 1 L', quantity: 2, price: 46.9 },
];

const TOTAL = ITEMS.reduce((sum, item) => sum + item.quantity * item.price, 0);

function Stage({ icon: Icon, who, title, delay, children }: {
  icon: LucideIcon;
  who: string;
  title: string;
  delay: string;
  children: React.ReactNode;
}) {
  return (
    <li
      className="rounded-xl border border-line bg-surface p-4 animate-in fade-in-0 slide-in-from-bottom-2 fill-mode-both duration-500"
      style={{ animationDelay: delay }}
    >
      <div className="flex items-center gap-2 text-sm">
        <Icon size={16} className="text-brand-600 dark:text-brand-300" aria-hidden="true" />
        <span className="font-semibold text-gray-900 dark:text-white">{title}</span>
        <span className="ml-auto text-xs text-gray-500 dark:text-gray-400">{who}</span>
      </div>
      <div className="mt-3">{children}</div>
    </li>
  );
}

/**
 * O caminho de uma venda, do balcão ao estoque, com peças de verdade. No lugar de
 * um print de tela, que envelhece a cada mudança no sistema e fica ilegível no celular.
 */
export function HeroScene() {
  return (
    <ol aria-label="Exemplo de uma venda: o orçamento feito no balcão é recebido no caixa e o estoque baixa sozinho" className="space-y-3">
      <Stage icon={FileText} title="Orçamento" who="Atendente" delay="0ms">
        <ul className="space-y-1.5 text-sm">
          {ITEMS.map((item) => (
            <li key={item.sku} className="flex items-baseline gap-2">
              <span className="w-5 shrink-0 tabular-nums text-gray-500 dark:text-gray-400">{item.quantity}×</span>
              <span className="min-w-0 flex-1 truncate text-gray-800 dark:text-gray-200">{item.name}</span>
              <span className="shrink-0 tabular-nums text-gray-800 dark:text-gray-200">
                {formatCurrency(item.quantity * item.price)}
              </span>
            </li>
          ))}
        </ul>
      </Stage>

      <Stage icon={Banknote} title="Caixa" who="Operador de caixa" delay="150ms">
        <div className="flex items-baseline justify-between gap-3">
          <span className="text-sm text-gray-600 dark:text-gray-300">Recebido no PIX</span>
          <span className="font-condensed text-3xl font-bold tabular-nums text-gray-900 dark:text-white">
            {formatCurrency(TOTAL)}
          </span>
        </div>
      </Stage>

      <Stage icon={Boxes} title="Estoque" who="Automático" delay="300ms">
        <div className="flex items-baseline justify-between gap-3 text-sm">
          <span className="min-w-0 truncate text-gray-800 dark:text-gray-200">Pneu traseiro Pirelli</span>
          <span className="shrink-0 tabular-nums">
            <span className="text-gray-500 line-through dark:text-gray-400">14</span>
            <span className="ml-2 font-semibold text-gray-900 dark:text-white">13 un</span>
          </span>
        </div>
      </Stage>
    </ol>
  );
}
