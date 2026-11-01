import { useState } from 'react';
import toast from 'react-hot-toast';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { fieldClass } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { cn } from '@/lib/utils';
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
    <Modal
      title={'Receber ' + transfer.transferNumber}
      description="Confira o que chegou de fato antes de confirmar."
      onClose={onClose}
      size="lg"
    >
        <ModalBody className="space-y-3">
          {transfer.items.map((item) => {
            const value = received[item.productId] ?? 0;
            const diff = value !== item.quantitySent;

            return (
              <div key={item.productId} className="flex items-center gap-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-gray-900 dark:text-white">
                    {item.productName ?? 'Produto'}
                  </p>
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    {item.sku} · enviado: {item.quantitySent}
                  </p>
                </div>

                <input
                  type="number"
                  inputMode="decimal"
                  aria-label={`Quantidade recebida de ${item.productName ?? 'produto'}`}
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
                  className={cn(fieldClass, 'w-24 text-right tabular-nums', diff && 'border-amber-500 bg-amber-50 dark:border-amber-500/60 dark:bg-amber-900/20')}
                />
              </div>
            );
          })}
        {divergent.length > 0 && (
          <div className="flex gap-2 rounded-md bg-amber-50 p-3 text-xs text-amber-800 dark:bg-amber-900/20 dark:text-amber-300">
            <AlertTriangle size={16} className="mt-0.5 shrink-0" />
            <p>
              {divergent.length} {divergent.length === 1 ? 'item confere' : 'itens conferem'} com
              quantidade diferente da enviada. A transferência será marcada com divergência, e a
              loja de origem resolve a diferença com um ajuste de estoque.
            </p>
          </div>
        )}

        </ModalBody>
        <ModalFooter>
          <Button variant="outline" onClick={onClose}>Cancelar</Button>
          <Button variant="success" onClick={handleConfirm} disabled={saving}>
            {saving ? 'Registrando…' : 'Confirmar recebimento'}
          </Button>
        </ModalFooter>
    </Modal>
  );
}
