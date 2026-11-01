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
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { PageHeader } from '@/components/ui/PageHeader';
import { confirmDialog, promptDialog } from '@/lib/dialogs';

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
    const confirmed = await confirmDialog({
      title: `Enviar ${transfer.transferNumber}`,
      description: 'O estoque sai desta loja agora.',
      confirmLabel: 'Enviar transferência',
    });
    if (!confirmed) return;
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
    const reason = await promptDialog({
      title: `Cancelar ${transfer.transferNumber}`,
      confirmLabel: 'Cancelar transferência',
      tone: 'danger',
      input: { label: 'Motivo do cancelamento', placeholder: 'Opcional' },
    });
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

  const transferActions = (t: StockTransfer) => (
    <>
      {direction === 'outbound' && t.status === 'Pending' && isAdmin && (
        <>
          <Button size="sm" variant="outline" onClick={() => handleCancel(t)}><X size={14} />Cancelar</Button>
          <Button size="sm" onClick={() => handleShip(t)}><Truck size={14} />Enviar</Button>
        </>
      )}
      {direction === 'inbound' && t.status === 'InTransit' && (
        <Button size="sm" variant="success" onClick={() => setReceiving(t)}>
          <ArrowDownLeft size={14} />
          Conferir e receber
        </Button>
      )}
    </>
  );

  const statusBadge = (t: StockTransfer) => (
    <span className={cn('whitespace-nowrap rounded-full px-2 py-1 text-xs font-medium', TRANSFER_STATUS_BADGE[t.status])}>
      {TRANSFER_STATUS_LABEL[t.status]}
    </span>
  );

  const number = (t: StockTransfer) => (
    <span className="font-medium text-gray-900 dark:text-white">
      {t.transferNumber}
      {t.hasDivergence && (
        <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-900/40 dark:text-amber-400">
          Divergência
        </span>
      )}
    </span>
  );

  const otherBranch = (t: StockTransfer) => branchName(direction === 'outbound' ? t.destinationBranchId : t.originBranchId);

  const columns: Column<StockTransfer>[] = [
    { key: 'number', header: 'Número', cell: number },
    { key: 'branch', header: direction === 'outbound' ? 'Destino' : 'Origem', cell: otherBranch },
    { key: 'items', header: 'Itens', align: 'right', cell: (t) => t.items.length },
    { key: 'status', header: 'Situação', cell: statusBadge },
    { key: 'date', header: 'Criada em', className: 'whitespace-nowrap', cell: (t) => formatDateTime(t.createdAt) },
    { key: 'actions', header: '', align: 'right', cell: (t) => <div className="flex justify-end gap-2">{transferActions(t)}</div> },
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="Transferências" subtitle="Movimentação de mercadoria entre as lojas.">
        {isAdmin && (
          <Button asChild>
            <Link to="/transfers/new"><Plus size={16} />Nova transferência</Link>
          </Button>
        )}
      </PageHeader>

      <div className="flex gap-1 border-b border-line">
        {([
          ['outbound', 'Enviadas', ArrowUpRight],
          ['inbound', 'Recebidas', ArrowDownLeft],
        ] as const).map(([value, label, Icon]) => (
          <button
            key={value}
            onClick={() => setDirection(value)}
            className={cn(
              'flex min-h-11 flex-1 items-center justify-center gap-2 border-b-2 px-4 text-sm font-medium transition-colors md:min-h-9 md:flex-none',
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

      <DataTable
        caption="Transferências"
        rows={transfers}
        columns={columns}
        rowKey={(t) => t.id}
        loading={loading}
        empty={
          <>
            <Truck className="mx-auto mb-3 text-gray-400" size={32} />
            {direction === 'outbound'
              ? 'Nenhuma transferência enviada por esta loja.'
              : 'Nenhuma transferência a caminho desta loja.'}
          </>
        }
        card={(t) => (
          <>
            <div className="flex items-start justify-between gap-2">
              {number(t)}
              {statusBadge(t)}
            </div>
            <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
              {direction === 'outbound' ? 'Para ' : 'De '}{otherBranch(t)}
            </div>
            <div className="text-xs text-gray-500 dark:text-gray-400">
              {t.items.length} {t.items.length === 1 ? 'item' : 'itens'} · {formatDateTime(t.createdAt)}
            </div>
            <div className="mt-3 flex gap-2 empty:hidden [&>*]:flex-1">{transferActions(t)}</div>
          </>
        )}
      />

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
