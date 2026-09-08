import { useState } from 'react';
import toast from 'react-hot-toast';
import { AlertTriangle, X } from 'lucide-react';
import { stockTransferService } from '@/services/stockTransferService';
import type { StockTransfer } from '@/types/stockTransfer';

interface Props {
  transfer: StockTransfer;
  onClose: () => void;
  onReceived: () => void;
}

/**
 * Conferência da carga que chegou.
 *
 * Cada linha começa com o que foi enviado, porque o caso comum é chegar tudo.
 * Digitar menos registra divergência — a diferença não some nem volta sozinha
 * para a origem: fica anotada para as duas lojas resolverem.
 */
export function ReceiveTransferModal({ transfer, onClose, onReceived }: Props) {
  const [received, setReceived] = useState<Record<string, number>>(
    Object.fromEntries(transfer.items.map((i) => [i.productId, i.quantitySent]))
  );
  const [saving, setSaving] = useState(false);

  const divergent = transfer.items.filter((i) => received[i.productId] !== i.quantitySent);

  async function handleConfirm() {
    setSaving(true);
    try {
      await stockTransferService.receive(transfer.id, {
        items: transfer.items.map((i) => ({
          productId: i.productId,
          quantityReceived: received[i.productId] ?? 0,
        })),
      });
      toast.success(
        divergent.length > 0
          ? 'Recebimento registrado com divergência.'
          : 'Transferência recebida.'
      );
      onReceived();
    } catch (e) {
      const msg = (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      toast.error(msg ?? 'Não foi possível registrar o recebimento.');
      setSaving(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="w-full max-w-lg rounded-xl bg-white shadow-xl dark:bg-gray-900">
        <div className="flex items-center justify-between border-b border-gray-200 px-5 py-4 dark:border-white/10">
          <div>
            <h2 className="font-semibold text-gray-900 dark:text-white">
              Receber {transfer.transferNumber}
            </h2>
            <p className="text-xs text-gray-500 dark:text-gray-400">
              Confira o que chegou de fato antes de confirmar.
            </p>
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-800 dark:hover:text-gray-200"
          >
            <X size={18} />
          </button>
        </div>

        <div className="max-h-80 space-y-3 overflow-y-auto p-5">
          {transfer.items.map((item) => {
            const value = received[item.productId] ?? 0;
            const diff = value !== item.quantitySent;

            return (
              <div key={item.productId} className="flex items-center gap-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-gray-900 dark:text-white">
                    {item.productName ?? 'Produto'}
                  </p>
                  <p className="text-xs text-gray-400 dark:text-gray-500">
                    {item.sku} · enviado: {item.quantitySent}
                  </p>
                </div>

                <input
                  type="number"
                  min={0}
                  max={item.quantitySent}
                  step="0.01"
                  value={value}
                  onChange={(e) =>
                    setReceived((prev) => ({
                      ...prev,
                      [item.productId]: Math.min(
                        item.quantitySent,
                        Math.max(0, Number(e.target.value) || 0)
                      ),
                    }))
                  }
                  className={`w-24 rounded-md border px-2 py-1.5 text-right text-sm tabular-nums ${
                    diff
                      ? 'border-amber-400 bg-amber-50 dark:bg-amber-900/20'
                      : 'border-gray-300 dark:border-white/15 dark:bg-white/5'
                  } text-gray-900 dark:text-white`}
                />
              </div>
            );
          })}
        </div>

        {divergent.length > 0 && (
          <div className="mx-5 mb-4 flex gap-2 rounded-md bg-amber-50 p-3 text-xs text-amber-800 dark:bg-amber-900/20 dark:text-amber-300">
            <AlertTriangle size={16} className="mt-0.5 shrink-0" />
            <p>
              {divergent.length} {divergent.length === 1 ? 'item confere' : 'itens conferem'} com
              quantidade diferente da enviada. A transferência será marcada com divergência, e a
              loja de origem resolve a diferença com um ajuste de estoque.
            </p>
          </div>
        )}

        <div className="flex justify-end gap-2 border-t border-gray-200 px-5 py-4 dark:border-white/10">
          <button
            onClick={onClose}
            className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:border-white/15 dark:text-gray-300 dark:hover:bg-white/5"
          >
            Cancelar
          </button>
          <button
            onClick={handleConfirm}
            disabled={saving}
            className="rounded-md bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
          >
            {saving ? 'Registrando…' : 'Confirmar recebimento'}
          </button>
        </div>
      </div>
    </div>
  );
}
