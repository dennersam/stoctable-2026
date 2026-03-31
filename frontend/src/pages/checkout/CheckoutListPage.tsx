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
      setSales(Array.isArray(data) ? data : (data as any).items ?? []);
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
        <h1 className="text-2xl font-bold text-gray-900">Caixa — Vendas Pendentes</h1>
        <button onClick={load} className="rounded border px-3 py-1.5 text-sm hover:bg-gray-50">
          Atualizar
        </button>
      </div>

      {loading ? (
        <div className="py-12 text-center text-gray-500">Carregando...</div>
      ) : sales.length === 0 ? (
        <div className="rounded-lg border border-gray-200 bg-white py-16 text-center text-gray-400">
          Nenhuma venda pendente de pagamento.
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {sales.map(s => (
            <div key={s.id} className="rounded-lg border border-gray-200 bg-white p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="font-mono text-sm font-semibold text-gray-700">{s.saleNumber}</span>
                <span className="rounded-full bg-orange-100 px-2 py-0.5 text-xs font-medium text-orange-700">
                  Pendente
                </span>
              </div>
              <div className="text-sm text-gray-600">{s.customerName || 'Cliente não identificado'}</div>
              <div className="text-sm">
                <span className="text-gray-400">Total: </span>
                <span className="font-bold text-blue-700">R$ {s.totalAmount.toFixed(2)}</span>
                {s.amountPaid > 0 && (
                  <span className="ml-2 text-green-600">(R$ {s.amountPaid.toFixed(2)} pago)</span>
                )}
              </div>
              <div className="text-xs text-gray-400">
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
