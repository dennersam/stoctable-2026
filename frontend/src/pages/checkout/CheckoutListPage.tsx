import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { saleService } from '@/services/saleService';
import type { Sale } from '@/types/sale';

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
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Caixa — Vendas Pendentes</h1>
        <button onClick={load} className="rounded border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800">
          Atualizar
        </button>
      </div>

      {loading ? (
        <div className="py-12 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
      ) : sales.length === 0 ? (
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 py-16 text-center text-gray-400 dark:text-gray-500">
          Nenhuma venda pendente de pagamento.
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {sales.map(s => (
            <div key={s.id} className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="font-mono text-sm font-semibold text-gray-700 dark:text-gray-200">{s.saleNumber}</span>
                <span className="rounded-full bg-orange-100 dark:bg-orange-900/40 px-2 py-0.5 text-xs font-medium text-orange-700 dark:text-orange-400">
                  Pendente
                </span>
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">{s.customerName || 'Cliente não identificado'}</div>
              <div className="text-sm">
                <span className="text-gray-400 dark:text-gray-500">Total: </span>
                <span className="font-bold text-brand-700 dark:text-brand-400">R$ {s.totalAmount.toFixed(2)}</span>
                {s.amountPaid > 0 && (
                  <span className="ml-2 text-green-600 dark:text-green-400">(R$ {s.amountPaid.toFixed(2)} pago)</span>
                )}
              </div>
              <div className="text-xs text-gray-400 dark:text-gray-500">
                {s.items.length} {s.items.length === 1 ? 'item' : 'itens'} •{' '}
                {new Date(s.createdAt).toLocaleDateString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
              </div>
              <Link
                to={`/checkout/${s.id}`}
                className="block w-full rounded bg-green-600 py-2 text-center text-sm font-medium text-white hover:bg-green-700"
              >
                Receber pagamento
              </Link>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
