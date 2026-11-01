import { useState, useEffect, useCallback } from 'react';
import toast from 'react-hot-toast';
import { productService } from '@/services/productService';
import { inventoryService } from '@/services/inventoryService';
import type { Product } from '@/types/product';
import type { InventoryMovement } from '@/types/inventory';
import { useBranchStore } from '@/store/branchStore';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field, SearchField } from '@/components/ui/Field';
import { fieldClass } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHeader';

// ─── helpers ────────────────────────────────────────────────────────────────

type StockStatus = 'ok' | 'low' | 'critical';

function getStockStatus(p: Product): StockStatus {
  const avail = p.stockQuantity - p.stockReserved;
  if (avail <= 0) return 'critical';
  if (avail <= p.stockMinimum) return 'low';
  return 'ok';
}

const STATUS_BADGE: Record<StockStatus, string> = {
  ok: 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-400',
  low: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-400',
  critical: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400',
};

const STATUS_LABEL: Record<StockStatus, string> = {
  ok: 'OK',
  low: 'Baixo',
  critical: 'Zerado',
};

// Espelha o enum MovementType do backend. QuotationReserve/QuotationRelease
// estavam aqui e não existem lá — reserva não gera movimento, mexe na coluna
// reserved. Transferência entra com as duas pernas.
const MOVEMENT_TYPE_LABEL: Record<string, string> = {
  AdjustmentIn: 'Entrada (ajuste)',
  AdjustmentOut: 'Saída (ajuste)',
  Sale: 'Venda',
  Purchase: 'Compra',
  TransferIn: 'Entrada (transferência)',
  TransferOut: 'Saída (transferência)',
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
  tone?: 'neutral' | 'warning' | 'critical';
}

function SummaryCard({ label, value, tone = 'neutral' }: SummaryCardProps) {
  const colors = {
    neutral: 'text-gray-900 dark:text-white',
    warning: 'text-amber-700 dark:text-amber-400',
    critical: 'text-red-700 dark:text-red-400',
  };

  return (
    <div className="rounded-lg border border-line bg-surface p-3 md:p-4">
      <p className="text-xs text-gray-500 dark:text-gray-400 md:text-sm">{label}</p>
      <p className={'mt-0.5 text-xl font-bold tabular-nums md:text-3xl ' + colors[tone]}>{value}</p>
    </div>
  );
}

// ─── main page ───────────────────────────────────────────────────────────────

export function InventoryPage() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [showCritical, setShowCritical] = useState(false);

  const activeBranch = useBranchStore((s) => s.active());

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

  const columns: Column<Product>[] = [
    {
      key: 'name',
      header: 'Produto',
      cell: (p) => (
        <>
          <div className="font-medium text-gray-900 dark:text-white">{p.name}</div>
          <div className="text-xs text-gray-500 dark:text-gray-400">
            <span className="font-mono">{p.sku}</span>{p.manufacturerName && ' · ' + p.manufacturerName}
          </div>
        </>
      ),
    },
    { key: 'available', header: 'Disponível', align: 'right', className: 'whitespace-nowrap font-semibold text-gray-900 dark:text-white', cell: (p) => (p.stockQuantity - p.stockReserved) + ' ' + p.unit },
    { key: 'total', header: 'Total', align: 'right', className: 'whitespace-nowrap', cell: (p) => p.stockQuantity + ' ' + p.unit },
    { key: 'reserved', header: 'Reservado', align: 'right', className: 'whitespace-nowrap', cell: (p) => (p.stockReserved > 0 ? p.stockReserved + ' ' + p.unit : '—') },
    { key: 'minimum', header: 'Mínimo', align: 'right', className: 'whitespace-nowrap', cell: (p) => p.stockMinimum + ' ' + p.unit },
    {
      key: 'status',
      header: 'Situação',
      cell: (p) => (
        <span className={'inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ' + STATUS_BADGE[getStockStatus(p)]}>
          {STATUS_LABEL[getStockStatus(p)]}
        </span>
      ),
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      className: 'whitespace-nowrap',
      cell: (p) => (
        <>
          <button onClick={() => openAdjust(p)} className="mr-3 font-medium text-brand-700 hover:underline dark:text-brand-300">Ajustar</button>
          <button onClick={() => openMovements(p)} className="text-gray-600 hover:underline dark:text-gray-300">Histórico</button>
        </>
      ),
    },
  ];

  // ─── render ─────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      {/* Sem o subtítulo o usuário não sabe de qual loja são os números — e agora
          cada loja tem os seus. */}
      <PageHeader
        title="Gestão de estoque"
        subtitle={<>Saldos de <span className="font-medium">{activeBranch?.name ?? 'sua loja'}</span>. Cada filial tem o próprio estoque.</>}
      />

      <div className="grid grid-cols-3 gap-2 md:gap-4">
        <SummaryCard label="Produtos" value={products.length} />
        <SummaryCard label="Estoque baixo" value={lowCount} tone="warning" />
        <SummaryCard label="Zerados" value={criticalCount} tone="critical" />
      </div>

      <div className="flex flex-wrap gap-2">
        <SearchField value={search} onChange={setSearch} placeholder="Buscar por SKU ou nome" className="min-w-48 flex-1" />
        <Button
          variant="outline"
          aria-pressed={showCritical}
          onClick={() => setShowCritical(!showCritical)}
          className={showCritical ? 'border-amber-400 bg-amber-50 text-amber-800 hover:bg-amber-100 dark:border-amber-500/50 dark:bg-amber-900/30 dark:text-amber-300 dark:hover:bg-amber-900/40' : ''}
        >
          Apenas críticos
        </Button>
      </div>

      <DataTable
        caption="Estoque por produto"
        rows={filtered}
        columns={columns}
        rowKey={(p) => p.id}
        loading={loading}
        empty={search || showCritical ? 'Nenhum produto com estes filtros.' : 'Nenhum produto cadastrado ainda.'}
        card={(p) => {
          const status = getStockStatus(p);
          return (
            <>
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="font-medium text-gray-900 dark:text-white">{p.name}</div>
                  <div className="text-xs text-gray-500 dark:text-gray-400">
                    {[p.sku, p.manufacturerName].filter(Boolean).join(' · ')}
                  </div>
                </div>
                <span className={'shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ' + STATUS_BADGE[status]}>
                  {STATUS_LABEL[status]}
                </span>
              </div>
              <dl className="mt-2 grid grid-cols-3 gap-2 text-sm tabular-nums">
                <div>
                  <dt className="text-xs text-gray-500 dark:text-gray-400">Disponível</dt>
                  <dd className="font-semibold text-gray-900 dark:text-white">{p.stockQuantity - p.stockReserved} {p.unit}</dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-500 dark:text-gray-400">Reservado</dt>
                  <dd className="text-gray-700 dark:text-gray-300">{p.stockReserved > 0 ? p.stockReserved + ' ' + p.unit : '—'}</dd>
                </div>
                <div>
                  <dt className="text-xs text-gray-500 dark:text-gray-400">Mínimo</dt>
                  <dd className="text-gray-700 dark:text-gray-300">{p.stockMinimum} {p.unit}</dd>
                </div>
              </dl>
              <div className="mt-3 grid grid-cols-2 gap-2">
                <Button variant="outline" onClick={() => openMovements(p)}>Histórico</Button>
                <Button variant="outline" onClick={() => openAdjust(p)}>Ajustar</Button>
              </div>
            </>
          );
        }}
      />

      {/* ── Adjust stock modal ─────────────────────────────────────────────── */}
      {adjustTarget && (
        <Modal title="Ajustar estoque" description={adjustTarget.name} onClose={() => setAdjustTarget(null)}>
          <ModalBody className="space-y-4">
            {/* Current stock info */}
            <div className="rounded-lg bg-gray-50 dark:bg-white/5 p-4">
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
              <div className="flex justify-between text-sm mt-1 pt-1 border-t border-line">
                <span className="text-gray-500 dark:text-gray-400">Disponível</span>
                <span className="font-bold text-gray-900 dark:text-white">
                  {adjustTarget.stockQuantity - adjustTarget.stockReserved} {adjustTarget.unit}
                </span>
              </div>
            </div>

            <div>
              <Field label="Quantidade" hint="Use negativo para retirada.">
                <input
                  type="number"
                  inputMode="decimal"
                  step="0.01"
                  value={adjustQty}
                  onChange={(e) => setAdjustQty(e.target.value)}
                  placeholder="Ex: 10 ou -5"
                  autoFocus
                  className={fieldClass}
                />
              </Field>
              {adjustQty && !isNaN(parseFloat(adjustQty)) && parseFloat(adjustQty) !== 0 && (
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                  Novo total:{' '}
                  <span className="font-semibold text-gray-900 dark:text-white">
                    {(adjustTarget.stockQuantity + parseFloat(adjustQty)).toFixed(2)} {adjustTarget.unit}
                  </span>
                </p>
              )}
            </div>

            <Field label="Motivo ou observação">
              <textarea
                value={adjustNotes}
                onChange={(e) => setAdjustNotes(e.target.value)}
                rows={2}
                placeholder="Ex: inventário mensal, devolução de cliente..."
                className={fieldClass + ' resize-none'}
              />
            </Field>

          </ModalBody>
          <ModalFooter>
            <Button variant="outline" onClick={() => setAdjustTarget(null)}>Cancelar</Button>
            <Button onClick={handleAdjust} disabled={adjustSaving}>
              {adjustSaving ? 'Salvando...' : 'Confirmar ajuste'}
            </Button>
          </ModalFooter>
        </Modal>
      )}

      {/* ── Movement history modal ─────────────────────────────────────────── */}
      {movementsTarget && (
        <Modal
          title="Histórico de movimentações"
          description={movementsTarget.name}
          onClose={() => { setMovementsTarget(null); setMovements([]); }}
          size="xl"
        >
          <ModalBody>
          {movementsLoading ? (
            <div className="py-10 text-center text-gray-500 dark:text-gray-400">Carregando...</div>
          ) : movements.length === 0 ? (
            <div className="py-10 text-center text-gray-500 dark:text-gray-400">
              Nenhuma movimentação registrada.
            </div>
          ) : (
            <div className="space-y-2">
              {movements.map((m) => (
                <div
                  key={m.id}
                  className="flex flex-col gap-2 rounded-lg border border-line px-4 py-3 sm:flex-row sm:items-start sm:justify-between sm:gap-4"
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
                        <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5 break-words">{m.notes}</p>
                      )}
                      <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                        {m.quantityBefore} → {m.quantityAfter} {movementsTarget.unit}
                      </p>
                    </div>
                  </div>
                  <div className="shrink-0 sm:text-right">
                    <p className="text-xs text-gray-500 dark:text-gray-400">{m.createdBy}</p>
                    <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{formatDate(m.createdAt)}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
          </ModalBody>
        </Modal>
      )}
    </div>
  );
}
