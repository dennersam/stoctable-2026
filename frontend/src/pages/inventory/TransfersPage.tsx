import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { ArrowDownLeft, ArrowUpRight, Plus, Truck, X } from 'lucide-react';
import { stockTransferService } from '@/services/stockTransferService';
import { useBranchStore } from '@/store/branchStore';
import { useAuthStore } from '@/store/authStore';
import {
  TRANSFER_STATUS_BADGE,
  TRANSFER_STATUS_LABEL,
  type StockTransfer,
} from '@/types/stockTransfer';
import { ReceiveTransferModal } from './ReceiveTransferModal';
import { formatDateTime } from '@/lib/utils';
import { cn } from '@/lib/utils';

type Direction = 'outbound' | 'inbound';

/**
 * Transferências entre lojas, nas duas direções.
 *
 * A separação em abas não é cosmética: o que se pode fazer com uma transferência
 * depende de qual ponta você é. Em "Enviadas" a loja é a origem e pode despachar
 * ou cancelar; em "Recebidas" ela é o destino e pode conferir a chegada.
 */
export function TransfersPage() {
  const [direction, setDirection] = useState<Direction>('outbound');
  const [transfers, setTransfers] = useState<StockTransfer[]>([]);
  const [loading, setLoading] = useState(true);
  const [receiving, setReceiving] = useState<StockTransfer | null>(null);

  const branches = useBranchStore((s) => s.branches);
  const isAdmin = useAuthStore((s) => s.user?.role === 'admin');

  const branchName = (id: string) => branches.find((b) => b.id === id)?.name ?? 'Outra loja';

  const load = useCallback(async () => {
    try {
      setTransfers(await stockTransferService.list(direction));
    } catch {
      toast.error('Não foi possível carregar as transferências.');
    } finally {
      setLoading(false);
    }
  }, [direction]);

  // A troca de aba dispara uma busca nova, e a anterior pode voltar depois. O
  // flag descarta a resposta obsoleta — sem ele, alternar rápido entre Enviadas
  // e Recebidas deixa a lista errada na tela.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const data = await stockTransferService.list(direction);
        if (!cancelled) setTransfers(data);
      } catch {
        if (!cancelled) toast.error('Não foi possível carregar as transferências.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [direction]);

  async function handleShip(transfer: StockTransfer) {
    if (!window.confirm(`Enviar a transferência ${transfer.transferNumber}? O estoque sai desta loja agora.`))
      return;
    try {
      await stockTransferService.ship(transfer.id);
      toast.success('Transferência enviada.');
      load();
    } catch (e) {
      const msg = (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não foi possível enviar a transferência.');
    }
  }

  async function handleCancel(transfer: StockTransfer) {
    const reason = window.prompt('Motivo do cancelamento:');
    if (reason === null) return;
    try {
      await stockTransferService.cancel(transfer.id, reason);
      toast.success('Transferência cancelada.');
      load();
    } catch (e) {
      const msg = (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não foi possível cancelar.');
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 dark:text-white">Transferências</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Movimentação de mercadoria entre as lojas.
          </p>
        </div>

        {isAdmin && (
          <Link
            to="/transfers/new"
            className="inline-flex items-center gap-2 rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
          >
            <Plus size={16} />
            Nova transferência
          </Link>
        )}
      </div>

      <div className="flex gap-1 border-b border-gray-200 dark:border-white/10">
        {([
          ['outbound', 'Enviadas', ArrowUpRight],
          ['inbound', 'Recebidas', ArrowDownLeft],
        ] as const).map(([value, label, Icon]) => (
          <button
            key={value}
            onClick={() => setDirection(value)}
            className={cn(
              'flex items-center gap-2 border-b-2 px-4 py-2 text-sm font-medium transition-colors',
              direction === value
                ? 'border-brand-600 text-brand-700 dark:border-brand-400 dark:text-brand-300'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200'
            )}
          >
            <Icon size={16} />
            {label}
          </button>
        ))}
      </div>

      {loading ? (
        <p className="py-10 text-center text-sm text-gray-500 dark:text-gray-400">Carregando…</p>
      ) : transfers.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 py-12 text-center dark:border-white/15">
          <Truck className="mx-auto mb-3 text-gray-300 dark:text-gray-600" size={32} />
          <p className="text-sm text-gray-500 dark:text-gray-400">
            {direction === 'outbound'
              ? 'Nenhuma transferência enviada por esta loja.'
              : 'Nenhuma transferência a caminho desta loja.'}
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-white/10">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500 dark:bg-white/5 dark:text-gray-400">
              <tr>
                <th className="px-4 py-3">Número</th>
                <th className="px-4 py-3">{direction === 'outbound' ? 'Destino' : 'Origem'}</th>
                <th className="px-4 py-3">Itens</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Criada em</th>
                <th className="px-4 py-3 text-right">Ações</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-white/10">
              {transfers.map((t) => (
                <tr key={t.id} className="hover:bg-gray-50 dark:hover:bg-white/5">
                  <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">
                    {t.transferNumber}
                    {t.hasDivergence && (
                      <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-medium text-amber-700 dark:bg-amber-900/40 dark:text-amber-400">
                        divergência
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-gray-600 dark:text-gray-300">
                    {branchName(direction === 'outbound' ? t.destinationBranchId : t.originBranchId)}
                  </td>
                  <td className="px-4 py-3 text-gray-600 dark:text-gray-300">{t.items.length}</td>
                  <td className="px-4 py-3">
                    <span className={cn('rounded-full px-2 py-1 text-xs font-medium', TRANSFER_STATUS_BADGE[t.status])}>
                      {TRANSFER_STATUS_LABEL[t.status]}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500 dark:text-gray-400">{formatDateTime(t.createdAt)}</td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-2">
                      {direction === 'outbound' && t.status === 'Pending' && isAdmin && (
                        <>
                          <button
                            onClick={() => handleShip(t)}
                            className="inline-flex items-center gap-1 rounded-md bg-brand-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-700"
                          >
                            <Truck size={14} />
                            Enviar
                          </button>
                          <button
                            onClick={() => handleCancel(t)}
                            className="inline-flex items-center gap-1 rounded-md border border-gray-300 px-3 py-1.5 text-xs text-gray-600 hover:bg-gray-50 dark:border-white/15 dark:text-gray-300 dark:hover:bg-white/5"
                          >
                            <X size={14} />
                            Cancelar
                          </button>
                        </>
                      )}

                      {direction === 'inbound' && t.status === 'InTransit' && (
                        <button
                          onClick={() => setReceiving(t)}
                          className="inline-flex items-center gap-1 rounded-md bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-700"
                        >
                          <ArrowDownLeft size={14} />
                          Conferir e receber
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {receiving && (
        <ReceiveTransferModal
          transfer={receiving}
          onClose={() => setReceiving(null)}
          onReceived={() => {
            setReceiving(null);
            load();
          }}
        />
      )}
    </div>
  );
}
