import { useState, useEffect, useCallback } from 'react';
import toast from 'react-hot-toast';
import { productService } from '@/services/productService';
import { inventoryService } from '@/services/inventoryService';
import type { Product } from '@/types/product';
import type { InventoryMovement } from '@/types/inventory';

// ─── helpers ────────────────────────────────────────────────────────────────

type StockStatus = 'ok' | 'low' | 'critical';

function getStockStatus(p: Product): StockStatus {
  const avail = p.stockQuantity - p.stockReserved;
  if (avail <= 0) return 'critical';
  if (avail <= p.stockMinimum) return 'low';
  return 'ok';
}

const STATUS_BADGE: Record<StockStatus, string> = {
  ok: 'bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-400',
  low: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/40 dark:text-yellow-400',
  critical: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400',
};

const STATUS_LABEL: Record<StockStatus, string> = {
  ok: 'OK',
  low: 'Baixo',
  critical: 'Zerado',
};

const MOVEMENT_TYPE_LABEL: Record<string, string> = {
  AdjustmentIn: 'Entrada (ajuste)',
  AdjustmentOut: 'Saída (ajuste)',
  Sale: 'Venda',
  QuotationReserve: 'Reserva (orçamento)',
  QuotationRelease: 'Liberação (orçamento)',
  Purchase: 'Compra',
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ─── sub-components ─────────────────────────────────────────────────────────

interface SummaryCardProps {
  label: string;
  value: number;
  color: 'blue' | 'yellow' | 'red';
}

function SummaryCard({ label, value, color }: SummaryCardProps) {
  const colors = {
    blue: 'border-blue-200 dark:border-blue-800 bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-400',
    yellow: 'border-yellow-200 dark:border-yellow-800 bg-yellow-50 dark:bg-yellow-900/20 text-yellow-700 dark:text-yellow-400',
    red: 'border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-400',
  };

  return (
    <div className={`rounded-lg border p-4 ${colors[color]}`}>
      <p className="text-sm font-medium opacity-80">{label}</p>
      <p className="mt-1 text-3xl font-bold">{value}</p>
    </div>
  );
}

interface ModalProps {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
  wide?: boolean;
}

function Modal({ title, onClose, children, wide }: ModalProps) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className={`w-full ${wide ? 'max-w-2xl' : 'max-w-md'} rounded-xl bg-white dark:bg-gray-900 shadow-xl`}>
        <div className="flex items-center justify-between border-b border-gray-200 dark:border-gray-700 px-5 py-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">{title}</h2>
          <button
            onClick={onClose}
            className="rounded-md p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600 dark:hover:text-gray-200"
          >
            ✕
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

// ─── main page ───────────────────────────────────────────────────────────────

export function InventoryPage() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [showCritical, setShowCritical] = useState(false);

  // Adjust modal
  const [adjustTarget, setAdjustTarget] = useState<Product | null>(null);
  const [adjustQty, setAdjustQty] = useState('');
  const [adjustNotes, setAdjustNotes] = useState('');
  const [adjustSaving, setAdjustSaving] = useState(false);

  // Movements modal
  const [movementsTarget, setMovementsTarget] = useState<Product | null>(null);
  const [movements, setMovements] = useState<InventoryMovement[]>([]);
  const [movementsLoading, setMovementsLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await productService.getAll({ pageSize: 500 });
      setProducts(Array.isArray(data) ? data : (data as any).items ?? []);
    } catch {
      toast.error('Erro ao carregar produtos.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const filtered = products.filter((p) => {
    const matchSearch =
      !search ||
      p.name.toLowerCase().includes(search.toLowerCase()) ||
      p.sku.toLowerCase().includes(search.toLowerCase());
    const matchCritical = !showCritical || getStockStatus(p) !== 'ok';
    return matchSearch && matchCritical;
  });

  // Summary counters
  const criticalCount = products.filter((p) => getStockStatus(p) === 'critical').length;
  const lowCount = products.filter((p) => getStockStatus(p) === 'low').length;

  // ── Adjust handlers ──────────────────────────────────────────────────────

  function openAdjust(p: Product) {
    setAdjustTarget(p);
    setAdjustQty('');
    setAdjustNotes('');
  }

  async function handleAdjust() {
    if (!adjustTarget) return;
    const qty = parseFloat(adjustQty);
    if (isNaN(qty) || qty === 0) {
      toast.error('Informe uma quantidade válida (diferente de zero).');
      return;
    }
    setAdjustSaving(true);
    try {
      await inventoryService.adjustStock({
        productId: adjustTarget.id,
        quantity: qty,
        notes: adjustNotes,
      });
      toast.success('Estoque ajustado com sucesso.');
      setAdjustTarget(null);
      load();
    } catch {
      toast.error('Erro ao ajustar estoque.');
    } finally {
      setAdjustSaving(false);
    }
  }

  // ── Movements handlers ───────────────────────────────────────────────────

  async function openMovements(p: Product) {
    setMovementsTarget(p);
    setMovements([]);
    setMovementsLoading(true);
    try {
      const data = await inventoryService.getMovements(p.id);
      setMovements(data);
    } catch {
      toast.error('Erro ao carregar movimentações.');
    } finally {
      setMovementsLoading(false);
    }
  }

  // ─── render ─────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Gestão de Estoque</h1>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <SummaryCard label="Total de produtos" value={products.length} color="blue" />
        <SummaryCard label="Estoque baixo" value={lowCount} color="yellow" />
        <SummaryCard label="Estoque zerado" value={criticalCount} color="red" />
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-2">
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Buscar por SKU ou nome..."
          className="flex-1 min-w-48 rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        <button
          onClick={() => setShowCritical(!showCritical)}
          className={`rounded-md px-4 py-2 text-sm font-medium transition-colors ${
            showCritical
              ? 'bg-yellow-100 dark:bg-yellow-900/40 text-yellow-700 dark:text-yellow-400 border border-yellow-300 dark:border-yellow-700'
              : 'bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-200 hover:bg-gray-200 dark:hover:bg-gray-600 border border-transparent'
          }`}
        >
          Apenas críticos
        </button>
        {(search || showCritical) && (
          <button
            onClick={() => { setSearch(''); setShowCritical(false); }}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            Limpar filtros
          </button>
        )}
      </div>

      {/* Table */}
      {loading ? (
        <div className="py-16 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                {['SKU', 'Nome', 'Total', 'Reservado', 'Disponível', 'Mínimo', 'Status', ''].map((h) => (
                  <th
                    key={h}
                    className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700 bg-white dark:bg-gray-900">
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={8} className="py-10 text-center text-gray-400 dark:text-gray-500">
                    Nenhum produto encontrado.
                  </td>
                </tr>
              ) : (
                filtered.map((p) => {
                  const avail = p.stockQuantity - p.stockReserved;
                  const status = getStockStatus(p);
                  return (
                    <tr key={p.id} className="hover:bg-gray-50 dark:hover:bg-gray-800/40 transition-colors">
                      <td className="px-4 py-3 font-mono text-sm text-gray-600 dark:text-gray-300">
                        {p.sku}
                      </td>
                      <td className="px-4 py-3">
                        <div className="font-medium text-gray-900 dark:text-white">{p.name}</div>
                        {p.manufacturerName && (
                          <div className="text-xs text-gray-400 dark:text-gray-500">{p.manufacturerName}</div>
                        )}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700 dark:text-gray-300">
                        {p.stockQuantity} {p.unit}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
                        {p.stockReserved > 0 ? `${p.stockReserved} ${p.unit}` : '—'}
                      </td>
                      <td className="px-4 py-3 text-sm font-semibold text-gray-900 dark:text-white">
                        {avail} {p.unit}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
                        {p.stockMinimum} {p.unit}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_BADGE[status]}`}>
                          {STATUS_LABEL[status]}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right text-sm whitespace-nowrap">
                        <button
                          onClick={() => openAdjust(p)}
                          className="mr-3 text-blue-600 dark:text-blue-400 hover:underline font-medium"
                        >
                          Ajustar
                        </button>
                        <button
                          onClick={() => openMovements(p)}
                          className="text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:underline"
                        >
                          Histórico
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* ── Adjust stock modal ─────────────────────────────────────────────── */}
      {adjustTarget && (
        <Modal title={`Ajustar Estoque — ${adjustTarget.name}`} onClose={() => setAdjustTarget(null)}>
          <div className="space-y-4">
            {/* Current stock info */}
            <div className="rounded-lg bg-gray-50 dark:bg-gray-800 p-4">
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Estoque total</span>
                <span className="font-semibold text-gray-900 dark:text-white">
                  {adjustTarget.stockQuantity} {adjustTarget.unit}
                </span>
              </div>
              <div className="flex justify-between text-sm mt-1">
                <span className="text-gray-500 dark:text-gray-400">Reservado</span>
                <span className="text-gray-700 dark:text-gray-300">
                  {adjustTarget.stockReserved} {adjustTarget.unit}
                </span>
              </div>
              <div className="flex justify-between text-sm mt-1 pt-1 border-t border-gray-200 dark:border-gray-700">
                <span className="text-gray-500 dark:text-gray-400">Disponível</span>
                <span className="font-bold text-gray-900 dark:text-white">
                  {adjustTarget.stockQuantity - adjustTarget.stockReserved} {adjustTarget.unit}
                </span>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Quantidade <span className="font-normal text-gray-400">(use negativo para retirada)</span>
              </label>
              <input
                type="number"
                step="0.01"
                value={adjustQty}
                onChange={(e) => setAdjustQty(e.target.value)}
                placeholder="Ex: 10 ou -5"
                autoFocus
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {adjustQty && !isNaN(parseFloat(adjustQty)) && parseFloat(adjustQty) !== 0 && (
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                  Novo total:{' '}
                  <span className="font-semibold text-gray-900 dark:text-white">
                    {(adjustTarget.stockQuantity + parseFloat(adjustQty)).toFixed(2)} {adjustTarget.unit}
                  </span>
                </p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Motivo / Observação
              </label>
              <textarea
                value={adjustNotes}
                onChange={(e) => setAdjustNotes(e.target.value)}
                rows={2}
                placeholder="Ex: inventário mensal, devolução de cliente..."
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
              />
            </div>

            <div className="flex gap-2 justify-end pt-1">
              <button
                onClick={() => setAdjustTarget(null)}
                className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                Cancelar
              </button>
              <button
                onClick={handleAdjust}
                disabled={adjustSaving}
                className="rounded-md bg-blue-600 hover:bg-blue-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
              >
                {adjustSaving ? 'Salvando...' : 'Confirmar ajuste'}
              </button>
            </div>
          </div>
        </Modal>
      )}

      {/* ── Movement history modal ─────────────────────────────────────────── */}
      {movementsTarget && (
        <Modal
          title={`Histórico de movimentações — ${movementsTarget.name}`}
          onClose={() => { setMovementsTarget(null); setMovements([]); }}
          wide
        >
          {movementsLoading ? (
            <div className="py-10 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
          ) : movements.length === 0 ? (
            <div className="py-10 text-center text-gray-400 dark:text-gray-500">
              Nenhuma movimentação registrada.
            </div>
          ) : (
            <div className="space-y-2 max-h-[60vh] overflow-y-auto pr-1">
              {movements.map((m) => (
                <div
                  key={m.id}
                  className="flex items-start justify-between rounded-lg border border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-4 py-3 gap-4"
                >
                  <div className="flex items-start gap-3 min-w-0">
                    <span
                      className={`inline-flex shrink-0 rounded-full px-2.5 py-0.5 text-xs font-bold mt-0.5 ${
                        m.quantity >= 0
                          ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                          : 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-400'
                      }`}
                    >
                      {m.quantity >= 0 ? '+' : ''}{m.quantity}
                    </span>
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-gray-900 dark:text-white">
                        {MOVEMENT_TYPE_LABEL[m.movementType] ?? m.movementType}
                      </p>
                      {m.notes && (
                        <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5 truncate">{m.notes}</p>
                      )}
                      <p className="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
                        {m.quantityBefore} → {m.quantityAfter} {movementsTarget.unit}
                      </p>
                    </div>
                  </div>
                  <div className="text-right shrink-0">
                    <p className="text-xs text-gray-500 dark:text-gray-400">{m.createdBy}</p>
                    <p className="text-xs text-gray-400 dark:text-gray-500 mt-0.5">{formatDate(m.createdAt)}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
