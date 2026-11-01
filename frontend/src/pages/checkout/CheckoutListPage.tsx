import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { RefreshCw } from 'lucide-react';
import { saleService } from '@/services/saleService';
import type { Sale } from '@/types/sale';
import { Button } from '@/components/ui/Button';
import { PageHeader } from '@/components/ui/PageHeader';
import { cn, formatCurrency, formatDateTime } from '@/lib/utils';

export function CheckoutListPage() {
  const [sales, setSales] = useState<Sale[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const data = await saleService.getAll({ status: 'pendingpayment' });
      setSales(data);
    } catch {
      toast.error('Erro ao carregar vendas.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="space-y-4">
      <PageHeader title="Caixa" subtitle="Vendas aguardando pagamento">
        <Button variant="outline" size="sm" onClick={load}>
          <RefreshCw size={16} className={cn(loading && 'animate-spin')} />
          Atualizar
        </Button>
      </PageHeader>

      {loading && sales.length === 0 ? (
        <div role="status" aria-label="Carregando" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map(i => <div key={i} className="h-44 animate-pulse rounded-lg bg-gray-200/70 dark:bg-white/5" />)}
        </div>
      ) : sales.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 py-16 text-center text-sm text-gray-500 dark:border-white/15 dark:text-gray-400">
          Nenhuma venda aguardando pagamento.
        </div>
      ) : (
        <div className={cn('grid gap-3 transition-opacity sm:grid-cols-2 lg:grid-cols-3', loading && 'opacity-60')}>
          {sales.map(s => (
            <div key={s.id} className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4">
              <div className="min-w-0">
                <div className="font-medium text-gray-900 dark:text-white">{s.customerName || 'Cliente não identificado'}</div>
                <div className="text-xs text-gray-500 dark:text-gray-400">
                  <span className="font-mono">{s.saleNumber}</span> · {s.items.length} {s.items.length === 1 ? 'item' : 'itens'} · {formatDateTime(s.createdAt)}
                </div>
              </div>
              <div className="mt-auto flex items-baseline justify-between gap-2 tabular-nums">
                <span className="text-xl font-bold text-gray-900 dark:text-white">{formatCurrency(s.totalAmount)}</span>
                {s.amountPaid > 0 && (
                  <span className="text-sm text-green-800 dark:text-green-400">{formatCurrency(s.amountPaid)} pago</span>
                )}
              </div>
              <Button asChild variant="success" className="w-full">
                <Link to={`/checkout/${s.id}`}>Receber pagamento</Link>
              </Button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
