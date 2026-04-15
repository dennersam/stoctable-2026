import { useState, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { quotationService } from '@/services/quotationService';
import { productService } from '@/services/productService';
import { customerService } from '@/services/customerService';
import type { Quotation } from '@/types/quotation';
import type { Product } from '@/types/product';
import type { Customer } from '@/types/customer';

interface CartRow {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  lineTotal: number;
}

const inputCls = 'w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 dark:placeholder-gray-500 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

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
      setCart(updated.items.map(i => ({
        productId: i.productId,
        sku: i.productSku,
        name: i.productName,
        quantity: i.quantity,
        unitPrice: i.unitPrice,
        discountPct: i.discountPct,
        lineTotal: i.lineTotal,
      })));
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
      setCart(updated.items.map(i => ({
        productId: i.productId,
        sku: i.productSku,
        name: i.productName,
        quantity: i.quantity,
        unitPrice: i.unitPrice,
        discountPct: i.discountPct,
        lineTotal: i.lineTotal,
      })));
    } catch (err: any) {
      toast.error(err?.response?.data?.detail ?? 'Erro ao atualizar quantidade.');
    }
  };

  const handleRemoveItem = async (itemId: string) => {
    if (!quotation) return;
    try {
      const updated = await quotationService.removeItem(quotation.id, itemId);
      setQuotation(updated);
      setCart(updated.items.map(i => ({
        productId: i.productId,
        sku: i.productSku,
        name: i.productName,
        quantity: i.quantity,
        unitPrice: i.unitPrice,
        discountPct: i.discountPct,
        lineTotal: i.lineTotal,
      })));
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
    setCustomerSearch(c.fullName);
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
      setCart(updated.items.map(i => ({
        productId: i.productId,
        sku: i.productSku,
        name: i.productName,
        quantity: i.quantity,
        unitPrice: i.unitPrice,
        discountPct: i.discountPct,
        lineTotal: i.lineTotal,
      })));
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

  return (
    <div className="flex h-full flex-col gap-4 lg:flex-row">
      {/* Left: item search + cart */}
      <div className="flex-1 space-y-4">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-bold text-gray-900 dark:text-white">
            Novo Orçamento
            {quotation && (
              <span className="ml-2 text-sm font-normal text-gray-400 dark:text-gray-500">{quotation.quotationNumber}</span>
            )}
            {creating && (
              <span className="ml-2 text-sm font-normal text-gray-400 dark:text-gray-500 animate-pulse">criando...</span>
            )}
          </h1>
        </div>

        {/* Customer selector */}
        <div className="relative">
          <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Cliente (opcional)</label>
          <input
            type="text"
            value={customerSearch}
            onChange={e => handleCustomerSearch(e.target.value)}
            placeholder="Buscar cliente por nome ou CPF/CNPJ..."
            className={inputCls}
          />
          {showCustomerDropdown && customerResults.length > 0 && (
            <div className="absolute z-10 mt-1 w-full rounded-md border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-lg">
              {customerResults.map(c => (
                <button
                  key={c.id}
                  onClick={() => handleSelectCustomer(c)}
                  className="flex w-full items-start gap-2 px-3 py-2 text-left hover:bg-brand-50 dark:hover:bg-brand-900/20"
                >
                  <div>
                    <div className="text-sm font-medium text-gray-900 dark:text-white">{c.fullName}</div>
                    <div className="text-xs text-gray-400 dark:text-gray-500">{c.documentNumber || 'Sem documento'}</div>
                  </div>
                </button>
              ))}
            </div>
          )}
          {customer && (
            <div className="mt-1 flex items-center gap-2">
              <span className="text-sm text-green-700 dark:text-green-400">✓ {customer.fullName}</span>
              <button
                onClick={handleRemoveCustomer}
                className="text-xs text-gray-400 hover:text-red-500"
              >
                Remover
              </button>
            </div>
          )}
        </div>

        {/* Product search */}
        <div className="relative">
          <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Adicionar produto</label>
          <input
            type="text"
            value={productSearch}
            onChange={e => handleProductSearch(e.target.value)}
            placeholder="SKU, código de barras ou nome do produto..."
            className={inputCls}
            autoComplete="off"
          />
          {showProductDropdown && productResults.length > 0 && (
            <div className="absolute z-10 mt-1 w-full rounded-md border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-lg">
              {productResults.map(p => (
                <button
                  key={p.id}
                  onClick={() => handleAddProduct(p)}
                  className="flex w-full items-center justify-between px-3 py-2 hover:bg-brand-50 dark:hover:bg-brand-900/20"
                >
                  <div>
                    <span className="font-mono text-xs text-gray-400 dark:text-gray-500 mr-2">{p.sku}</span>
                    <span className="text-sm text-gray-900 dark:text-white">{p.name}</span>
                  </div>
                  <div className="text-right">
                    <div className="text-sm font-medium text-gray-900 dark:text-white">R$ {p.salePrice.toFixed(2)}</div>
                    <div className="text-xs text-gray-400 dark:text-gray-500">
                      Disp: {(p.stockQuantity - p.stockReserved).toFixed(0)} {p.unit}
                    </div>
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Cart table */}
        <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase text-gray-500 dark:text-gray-400">Produto</th>
                <th className="w-24 px-3 py-2 text-center text-xs font-medium uppercase text-gray-500 dark:text-gray-400">Qtd</th>
                <th className="px-3 py-2 text-right text-xs font-medium uppercase text-gray-500 dark:text-gray-400">Unit.</th>
                <th className="px-3 py-2 text-right text-xs font-medium uppercase text-gray-500 dark:text-gray-400">Total</th>
                <th className="w-8 px-3 py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700 bg-white dark:bg-gray-900">
              {cart.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-gray-400 dark:text-gray-500 text-sm">
                    Nenhum item. Busque um produto acima.
                  </td>
                </tr>
              ) : (
                cart.map(row => {
                  const item = quotation?.items.find(i => i.productId === row.productId);
                  return (
                    <tr key={row.productId}>
                      <td className="px-3 py-2">
                        <div className="text-sm font-medium text-gray-900 dark:text-white">{row.name}</div>
                        <div className="text-xs text-gray-400 dark:text-gray-500">{row.sku}</div>
                      </td>
                      <td className="px-3 py-2">
                        <input
                          type="number"
                          min={1}
                          value={row.quantity}
                          onChange={e => handleUpdateQuantity(row.productId, Number(e.target.value))}
                          className="w-20 rounded border border-gray-200 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white px-2 py-1 text-center text-sm"
                        />
                      </td>
                      <td className="px-3 py-2 text-right text-sm text-gray-600 dark:text-gray-300">
                        R$ {row.unitPrice.toFixed(2)}
                      </td>
                      <td className="px-3 py-2 text-right text-sm font-medium text-gray-900 dark:text-white">
                        R$ {row.lineTotal.toFixed(2)}
                      </td>
                      <td className="px-3 py-2">
                        {item && (
                          <button
                            onClick={() => handleRemoveItem(item.id)}
                            className="text-red-400 hover:text-red-600"
                            title="Remover"
                          >
                            ✕
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Right: summary + actions */}
      <div className="w-full space-y-4 lg:w-72">
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 space-y-3">
          <h2 className="font-semibold text-gray-900 dark:text-white">Resumo</h2>
          <div className="flex justify-between text-sm text-gray-600 dark:text-gray-300">
            <span>Subtotal</span>
            <span>R$ {subtotal.toFixed(2)}</span>
          </div>
          {discountPct > 0 && (
            <div className="flex justify-between text-sm text-red-600 dark:text-red-400">
              <span>Desconto ({discountPct}%)</span>
              <span>- R$ {discountAmount.toFixed(2)}</span>
            </div>
          )}
          <div className="flex justify-between border-t border-gray-100 dark:border-gray-700 pt-2 font-bold text-gray-900 dark:text-white">
            <span>Total</span>
            <span className="text-brand-700 dark:text-brand-400">R$ {total.toFixed(2)}</span>
          </div>
        </div>

        <button
          disabled={cart.length === 0 || saving}
          onClick={() => setShowFinalize(true)}
          className="w-full rounded-md bg-brand-600 py-3 text-sm font-semibold text-white hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-40"
        >
          Finalizar orçamento
        </button>

        <button
          disabled={saving}
          onClick={() => quotation ? setShowCancel(true) : navigate('/quotations')}
          className="w-full rounded-md border border-red-200 dark:border-red-800 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 disabled:opacity-40"
        >
          {quotation ? 'Cancelar orçamento' : 'Voltar'}
        </button>
      </div>

      {/* Finalize dialog */}
      {showFinalize && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white dark:bg-gray-900 p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Finalizar orçamento</h3>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Desconto geral (%)</label>
              <input
                type="number"
                min={0}
                max={100}
                step={0.5}
                value={discountPct}
                onChange={e => setDiscountPct(Number(e.target.value))}
                className={inputCls}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Observações</label>
              <textarea
                value={finalizeNotes}
                onChange={e => setFinalizeNotes(e.target.value)}
                rows={3}
                className={`${inputCls} resize-none`}
                placeholder="Opcional..."
              />
            </div>
            <div className="rounded-md bg-brand-50 dark:bg-brand-900/20 p-3 text-sm">
              <div className="flex justify-between text-gray-700 dark:text-gray-300">
                <span>Subtotal:</span><span>R$ {subtotal.toFixed(2)}</span>
              </div>
              {discountPct > 0 && (
                <div className="flex justify-between text-red-600 dark:text-red-400">
                  <span>Desconto:</span><span>- R$ {(subtotal * discountPct / 100).toFixed(2)}</span>
                </div>
              )}
              <div className="flex justify-between font-bold mt-1 border-t border-brand-100 dark:border-brand-800 pt-1 text-gray-900 dark:text-white">
                <span>Total:</span>
                <span>R$ {(subtotal - subtotal * discountPct / 100).toFixed(2)}</span>
              </div>
            </div>
            <div className="flex gap-2 justify-end">
              <button
                onClick={() => setShowFinalize(false)}
                className="rounded border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                Voltar
              </button>
              <button
                onClick={handleFinalize}
                disabled={saving}
                className="rounded bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
              >
                {saving ? 'Finalizando...' : 'Confirmar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Cancel dialog */}
      {showCancel && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white dark:bg-gray-900 p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-semibold text-red-700 dark:text-red-400">Cancelar orçamento</h3>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Motivo *</label>
              <textarea
                value={cancelReason}
                onChange={e => setCancelReason(e.target.value)}
                rows={3}
                className={`${inputCls} resize-none`}
                placeholder="Informe o motivo do cancelamento..."
              />
            </div>
            <div className="flex gap-2 justify-end">
              <button
                onClick={() => setShowCancel(false)}
                className="rounded border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                Voltar
              </button>
              <button
                onClick={handleCancel}
                disabled={saving || !cancelReason.trim()}
                className="rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                {saving ? 'Cancelando...' : 'Confirmar cancelamento'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
