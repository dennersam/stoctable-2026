import { useState, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Trash2, UserRound, X } from 'lucide-react';
import { quotationService } from '@/services/quotationService';
import { productService } from '@/services/productService';
import { customerService } from '@/services/customerService';
import type { Quotation } from '@/types/quotation';
import type { Product } from '@/types/product';
import type { Customer } from '@/types/customer';
import { Button } from '@/components/ui/Button';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { Field, SearchField } from '@/components/ui/Field';
import { fieldClass } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHeader';
import { QuantityStepper } from '@/components/ui/QuantityStepper';
import { formatCurrency } from '@/lib/utils';

interface CartRow {
  productId: string;
  itemId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  lineTotal: number;
}

function toRows(quotation: Quotation): CartRow[] {
  return quotation.items.map(i => ({
    productId: i.productId,
    itemId: i.id,
    sku: i.productSku,
    name: i.productName,
    quantity: i.quantity,
    unitPrice: i.unitPrice,
    discountPct: i.discountPct,
    lineTotal: i.lineTotal,
  }));
}

/** Lista de resultados da busca. O mousedown não tira o foco do campo, senão ela fecharia antes do clique. */
function SearchResults({ children }: { children: React.ReactNode }) {
  return (
    <div
      onMouseDown={e => e.preventDefault()}
      className="absolute z-20 mt-1 max-h-80 w-full overflow-y-auto rounded-md border border-line bg-raised shadow-lg"
    >
      {children}
    </div>
  );
}

const RESULT_ROW = 'flex min-h-11 w-full items-center justify-between gap-3 px-3 py-2 text-left hover:bg-brand-50 dark:hover:bg-white/5';

export function QuotationEditorPage() {
  const navigate = useNavigate();

  const [quotation, setQuotation] = useState<Quotation | null>(null);
  const [cart, setCart] = useState<CartRow[]>([]);
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [creating, setCreating] = useState(false);

  // Customer selected before the quotation was created is stored here and
  // applied as soon as the quotation is lazily created.
  const pendingCustomerRef = useRef<Customer | null>(null);

  const [productSearch, setProductSearch] = useState('');
  const [productResults, setProductResults] = useState<Product[]>([]);
  const [showProductDropdown, setShowProductDropdown] = useState(false);
  const searchTimeout = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const [customerSearch, setCustomerSearch] = useState('');
  const [customerResults, setCustomerResults] = useState<Customer[]>([]);
  const [showCustomerDropdown, setShowCustomerDropdown] = useState(false);
  const customerTimeout = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const [showFinalize, setShowFinalize] = useState(false);
  const [discountPct, setDiscountPct] = useState(0);
  const [finalizeNotes, setFinalizeNotes] = useState('');

  const [showCancel, setShowCancel] = useState(false);
  const [cancelReason, setCancelReason] = useState('');

  const [saving, setSaving] = useState(false);

  // Quantidades ainda indo para o servidor. Finalizar antes disso fecharia o
  // orçamento com o total anterior à última mudança.
  const [quantitiesBusy, setQuantitiesBusy] = useState(0);
  const handleQuantityBusy = useCallback((delta: 1 | -1) => setQuantitiesBusy(n => n + delta), []);
  // Trocar a chave remonta o seletor quando o servidor recusa, para voltar ao valor real.
  const [stepperEpoch, setStepperEpoch] = useState(0);

  // ── Lazy creation ────────────────────────────────────────────────────────────
  // The quotation is only created when the user actually adds a product (or
  // selects a customer while items are already being added).  This prevents
  // orphan draft records when the user opens the page and navigates away.
  const ensureQuotation = useCallback(async (): Promise<Quotation | null> => {
    if (quotation) return quotation;
    setCreating(true);
    try {
      const q = await quotationService.create({ customerId: undefined });
      setQuotation(q);
      setCart([]);
      // Apply a customer that was selected before the quotation existed
      if (pendingCustomerRef.current) {
        try {
          await quotationService.setCustomer(q.id, pendingCustomerRef.current.id);
        } catch { /* non-critical — customer can be set again */ }
      }
      return q;
    } catch {
      toast.error('Erro ao criar orçamento.');
      navigate('/quotations');
      return null;
    } finally {
      setCreating(false);
    }
  }, [quotation, navigate]);

  const handleProductSearch = useCallback((value: string) => {
    setProductSearch(value);
    clearTimeout(searchTimeout.current);
    if (!value.trim()) { setProductResults([]); setShowProductDropdown(false); return; }
    searchTimeout.current = setTimeout(async () => {
      try {
        const results = await productService.search(value.trim());
        setProductResults(results.slice(0, 8));
        setShowProductDropdown(true);
      } catch { /* noop */ }
    }, 300);
  }, []);

  const handleAddProduct = async (product: Product) => {
    setShowProductDropdown(false);
    setProductSearch('');
    const q = await ensureQuotation();
    if (!q) return;
    try {
      const updated = await quotationService.addItem(q.id, {
        productId: product.id,
        quantity: 1,
        discountPct: 0,
      });
      setQuotation(updated);
      setCart(toRows(updated));
      toast.success(`${product.name} adicionado.`);
    } catch (err: any) {
      toast.error(err?.response?.data?.detail ?? 'Erro ao adicionar item.');
    }
  };

  const handleUpdateQuantity = async (productId: string, quantity: number) => {
    if (!quotation || quantity <= 0) return;
    try {
      const updated = await quotationService.addItem(quotation.id, { productId, quantity, discountPct: 0 });
      setQuotation(updated);
      setCart(toRows(updated));
    } catch (err: any) {
      toast.error(err?.response?.data?.detail ?? 'Erro ao atualizar quantidade.');
      setStepperEpoch(n => n + 1);
    }
  };

  const handleRemoveItem = async (itemId: string) => {
    if (!quotation) return;
    try {
      const updated = await quotationService.removeItem(quotation.id, itemId);
      setQuotation(updated);
      setCart(toRows(updated));
    } catch {
      toast.error('Erro ao remover item.');
    }
  };

  const handleCustomerSearch = useCallback((value: string) => {
    setCustomerSearch(value);
    clearTimeout(customerTimeout.current);
    if (!value.trim()) { setCustomerResults([]); setShowCustomerDropdown(false); return; }
    customerTimeout.current = setTimeout(async () => {
      try {
        const data = await customerService.getAll({ search: value.trim() });
        const items = Array.isArray(data) ? data : (data as any).items ?? [];
        setCustomerResults(items.slice(0, 6));
        setShowCustomerDropdown(true);
      } catch { /* noop */ }
    }, 300);
  }, []);

  const handleSelectCustomer = async (c: Customer) => {
    setCustomer(c);
    setCustomerSearch('');
    setShowCustomerDropdown(false);
    if (quotation) {
      try {
        await quotationService.setCustomer(quotation.id, c.id);
      } catch {
        toast.error('Erro ao vincular cliente ao orçamento.');
      }
    } else {
      // Store for when the quotation is lazily created
      pendingCustomerRef.current = c;
    }
  };

  const handleRemoveCustomer = async () => {
    setCustomer(null);
    setCustomerSearch('');
    pendingCustomerRef.current = null;
    if (!quotation) return;
    try {
      await quotationService.setCustomer(quotation.id, null);
    } catch { /* noop */ }
  };

  const handleFinalize = async () => {
    if (!quotation) return;
    setSaving(true);
    try {
      const updated = await quotationService.finalize(quotation.id, {
        discountPct,
        notes: finalizeNotes || undefined,
      });
      setQuotation(updated);
      setCart(toRows(updated));
      setShowFinalize(false);
      toast.success(`Orçamento ${updated.quotationNumber} finalizado!`);
      navigate('/quotations');
    } catch (err: any) {
      toast.error(err?.response?.data?.detail ?? 'Erro ao finalizar orçamento.');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = async () => {
    if (!quotation) {
      // No quotation created yet — just go back without leaving any orphan
      navigate('/quotations');
      return;
    }
    if (!cancelReason.trim()) {
      toast.error('Informe o motivo do cancelamento.');
      return;
    }
    setSaving(true);
    try {
      await quotationService.cancel(quotation.id, { cancellationReason: cancelReason });
      toast.success('Orçamento cancelado.');
      navigate('/quotations');
    } catch {
      toast.error('Erro ao cancelar orçamento.');
    } finally {
      setSaving(false);
    }
  };

  const subtotal = cart.reduce((s, r) => s + r.lineTotal, 0);
  const discountAmount = subtotal * discountPct / 100;
  const total = subtotal - discountAmount;
  const canFinalize = cart.length > 0 && !saving && quantitiesBusy === 0;

  const stepper = (row: CartRow) => (
    <QuantityStepper
      key={row.productId + stepperEpoch}
      value={row.quantity}
      label={`Quantidade de ${row.name}`}
      onChange={quantity => handleUpdateQuantity(row.productId, quantity)}
      onBusy={handleQuantityBusy}
    />
  );

  const removeButton = (row: CartRow, cls: string) => (
    <button
      type="button"
      aria-label={`Remover ${row.name}`}
      title="Remover"
      onClick={() => handleRemoveItem(row.itemId)}
      className={`flex items-center justify-center rounded-md text-gray-500 hover:bg-red-50 hover:text-red-700 dark:text-gray-400 dark:hover:bg-red-900/30 dark:hover:text-red-300 ${cls}`}
    >
      <Trash2 size={16} />
    </button>
  );

  const columns: Column<CartRow>[] = [
    {
      key: 'product',
      header: 'Produto',
      cell: row => (
        <>
          <div className="font-medium text-gray-900 dark:text-white">{row.name}</div>
          <div className="font-mono text-xs text-gray-500 dark:text-gray-400">{row.sku}</div>
        </>
      ),
    },
    { key: 'quantity', header: 'Quantidade', align: 'center', cell: stepper },
    { key: 'unit', header: 'Unitário', align: 'right', className: 'whitespace-nowrap', cell: row => formatCurrency(row.unitPrice) },
    { key: 'total', header: 'Total', align: 'right', className: 'whitespace-nowrap font-medium text-gray-900 dark:text-white', cell: row => formatCurrency(row.lineTotal) },
    { key: 'remove', header: '', align: 'right', cell: row => removeButton(row, 'h-8 w-8') },
  ];

  return (
    <div className="flex min-h-full flex-col gap-4 lg:flex-row lg:items-start">
      {/* Left: item search + cart */}
      <div className="min-w-0 flex-1 space-y-4">
        <PageHeader
          title="Novo orçamento"
          subtitle={creating ? 'Criando...' : quotation?.quotationNumber}
        >
          {quotation ? (
            <Button variant="danger-outline" size="sm" disabled={saving} onClick={() => setShowCancel(true)}>
              Cancelar orçamento
            </Button>
          ) : (
            <Button variant="outline" size="sm" onClick={() => navigate('/quotations')}>Voltar</Button>
          )}
        </PageHeader>

        {/* Customer selector */}
        {customer ? (
          <div className="flex items-center gap-3 rounded-md border border-line bg-surface py-1 pl-3 pr-1">
            <UserRound size={18} className="shrink-0 text-gray-500 dark:text-gray-400" />
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-medium text-gray-900 dark:text-white">{customer.fullName}</div>
              {customer.documentNumber && (
                <div className="text-xs text-gray-500 dark:text-gray-400">{customer.documentNumber}</div>
              )}
            </div>
            <button
              type="button"
              aria-label="Remover cliente do orçamento"
              onClick={handleRemoveCustomer}
              className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-white/10"
            >
              <X size={16} />
            </button>
          </div>
        ) : (
          <div className="relative" onBlur={() => setShowCustomerDropdown(false)}>
            <SearchField
              value={customerSearch}
              onChange={handleCustomerSearch}
              placeholder="Cliente (opcional): nome ou CPF/CNPJ"
            />
            {showCustomerDropdown && customerResults.length > 0 && (
              <SearchResults>
                {customerResults.map(c => (
                  <button key={c.id} type="button" onClick={() => handleSelectCustomer(c)} className={RESULT_ROW}>
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-medium text-gray-900 dark:text-white">{c.fullName}</span>
                      <span className="block text-xs text-gray-500 dark:text-gray-400">{c.documentNumber || 'Sem documento'}</span>
                    </span>
                  </button>
                ))}
              </SearchResults>
            )}
          </div>
        )}

        {/* Product search */}
        <div className="relative" onBlur={() => setShowProductDropdown(false)}>
          <SearchField
            value={productSearch}
            onChange={handleProductSearch}
            placeholder="Adicionar produto: nome, SKU ou código"
          />
          {showProductDropdown && productResults.length > 0 && (
            <SearchResults>
              {productResults.map(p => (
                <button key={p.id} type="button" onClick={() => handleAddProduct(p)} className={RESULT_ROW}>
                  <span className="min-w-0">
                    <span className="block text-sm text-gray-900 dark:text-white">{p.name}</span>
                    <span className="block font-mono text-xs text-gray-500 dark:text-gray-400">{p.sku}</span>
                  </span>
                  <span className="shrink-0 text-right">
                    <span className="block text-sm font-medium tabular-nums text-gray-900 dark:text-white">{formatCurrency(p.salePrice)}</span>
                    <span className="block text-xs tabular-nums text-gray-500 dark:text-gray-400">
                      Disp: {(p.stockQuantity - p.stockReserved).toFixed(0)} {p.unit}
                    </span>
                  </span>
                </button>
              ))}
            </SearchResults>
          )}
        </div>

        {/* Cart */}
        <DataTable
          caption="Itens do orçamento"
          rows={cart}
          columns={columns}
          rowKey={row => row.productId}
          empty="Nenhum item ainda. Busque um produto acima para começar."
          card={row => (
            <>
              <div className="flex items-start gap-2">
                <div className="min-w-0 flex-1">
                  <div className="font-medium text-gray-900 dark:text-white">{row.name}</div>
                  <div className="text-xs text-gray-500 dark:text-gray-400">
                    <span className="font-mono">{row.sku}</span> · {formatCurrency(row.unitPrice)} cada
                  </div>
                </div>
                {removeButton(row, '-mr-1 -mt-1 h-11 w-11 shrink-0')}
              </div>
              <div className="mt-2 flex items-center justify-between gap-3">
                {stepper(row)}
                <span className="text-base font-semibold tabular-nums text-gray-900 dark:text-white">{formatCurrency(row.lineTotal)}</span>
              </div>
            </>
          )}
        />
      </div>

      {/* Desktop: resumo ao lado dos itens */}
      <aside className="hidden w-72 shrink-0 space-y-3 lg:block">
        <div className="rounded-lg border border-line bg-surface p-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">Resumo</h2>
          <div className="mt-3 flex items-baseline justify-between">
            <span className="text-sm text-gray-600 dark:text-gray-300">{cart.length} {cart.length === 1 ? 'item' : 'itens'}</span>
            <span className="text-2xl font-bold tabular-nums text-gray-900 dark:text-white">{formatCurrency(subtotal)}</span>
          </div>
        </div>
        <Button size="lg" className="w-full" disabled={!canFinalize} onClick={() => setShowFinalize(true)}>
          Finalizar orçamento
        </Button>
      </aside>

      {/* Celular e tablet: total e ação sempre ao alcance do polegar */}
      <div className="sticky -bottom-4 -mx-4 -mb-4 mt-auto flex items-center justify-between gap-3 border-t border-line bg-surface px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] md:-bottom-6 md:-mx-6 md:-mb-6 md:px-6 lg:hidden">
        <div className="min-w-0">
          <div className="text-xs text-gray-500 dark:text-gray-400">{cart.length} {cart.length === 1 ? 'item' : 'itens'}</div>
          <div className="text-lg font-bold tabular-nums text-gray-900 dark:text-white">{formatCurrency(subtotal)}</div>
        </div>
        <Button disabled={!canFinalize} onClick={() => setShowFinalize(true)}>Finalizar orçamento</Button>
      </div>

      {/* Finalize dialog */}
      {showFinalize && (
        <Modal title="Finalizar orçamento" onClose={() => setShowFinalize(false)}>
          <ModalBody className="space-y-4">
            <Field label="Desconto geral (%)">
              <input
                type="number"
                inputMode="decimal"
                min={0}
                max={100}
                step={0.5}
                value={discountPct}
                onChange={e => setDiscountPct(Number(e.target.value))}
                className={fieldClass}
              />
            </Field>
            <Field label="Observações">
              <textarea
                value={finalizeNotes}
                onChange={e => setFinalizeNotes(e.target.value)}
                rows={3}
                className={`${fieldClass} resize-none`}
                placeholder="Opcional"
              />
            </Field>
            <dl className="space-y-1 rounded-md bg-gray-50 p-3 text-sm tabular-nums dark:bg-white/5">
              <div className="flex justify-between text-gray-600 dark:text-gray-300">
                <dt>Subtotal</dt><dd>{formatCurrency(subtotal)}</dd>
              </div>
              {discountPct > 0 && (
                <div className="flex justify-between text-gray-600 dark:text-gray-300">
                  <dt>Desconto ({discountPct}%)</dt><dd>- {formatCurrency(discountAmount)}</dd>
                </div>
              )}
              <div className="flex justify-between border-t border-line pt-1 text-base font-bold text-gray-900 dark:text-white">
                <dt>Total</dt><dd>{formatCurrency(total)}</dd>
              </div>
            </dl>
          </ModalBody>
          <ModalFooter>
            <Button variant="outline" onClick={() => setShowFinalize(false)}>Voltar</Button>
            <Button onClick={handleFinalize} disabled={saving}>{saving ? 'Finalizando...' : 'Finalizar orçamento'}</Button>
          </ModalFooter>
        </Modal>
      )}

      {/* Cancel dialog */}
      {showCancel && (
        <Modal title="Cancelar orçamento" onClose={() => setShowCancel(false)}>
          <ModalBody>
            <Field label="Motivo" required>
              <textarea
                autoFocus
                value={cancelReason}
                onChange={e => setCancelReason(e.target.value)}
                rows={3}
                className={`${fieldClass} resize-none`}
                placeholder="Informe o motivo do cancelamento"
              />
            </Field>
          </ModalBody>
          <ModalFooter>
            <Button variant="outline" onClick={() => setShowCancel(false)}>Voltar</Button>
            <Button variant="danger" onClick={handleCancel} disabled={saving || !cancelReason.trim()}>
              {saving ? 'Cancelando...' : 'Cancelar orçamento'}
            </Button>
          </ModalFooter>
        </Modal>
      )}
    </div>
  );
}
