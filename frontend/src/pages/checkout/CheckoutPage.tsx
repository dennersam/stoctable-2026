import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { saleService } from '@/services/saleService';
import type { Sale, PaymentMethod } from '@/types/sale';
import { CurrencyInput } from '@/components/ui/CurrencyInput';

interface PaymentEntry {
  paymentMethodId: string;
  amount: number;
  installments: number;
  transactionRef: string;
}

const fieldCls = 'w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

export function CheckoutPage() {
  const { saleId } = useParams<{ saleId: string }>();
  const navigate = useNavigate();

  const [sale, setSale] = useState<Sale | null>(null);
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethod[]>([]);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(false);

  const [payments, setPayments] = useState<PaymentEntry[]>([
    { paymentMethodId: '', amount: 0, installments: 1, transactionRef: '' },
  ]);

  const [showCancel, setShowCancel] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  useEffect(() => {
    if (!saleId) return;
    const load = async () => {
      try {
        const [s, methods] = await Promise.all([
          saleService.getById(saleId),
          saleService.getPaymentMethods(),
        ]);
        setSale(s);
        setPaymentMethods(methods);
        setPayments([{
          paymentMethodId: methods[0]?.id ?? '',
          amount: s.totalAmount - s.amountPaid,
          installments: 1,
          transactionRef: '',
        }]);
      } catch {
        toast.error('Erro ao carregar venda.');
        navigate('/checkout');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [saleId, navigate]);

  const totalPayments = payments.reduce((s, p) => s + (Number(p.amount) || 0), 0);
  const remaining = sale ? sale.totalAmount - sale.amountPaid - totalPayments : 0;

  const addPaymentRow = () => {
    setPayments(prev => [...prev, {
      paymentMethodId: paymentMethods[0]?.id ?? '',
      amount: Math.max(0, remaining),
      installments: 1,
      transactionRef: '',
    }]);
  };

  const removePaymentRow = (idx: number) => {
    setPayments(prev => prev.filter((_, i) => i !== idx));
  };

  const updatePayment = (idx: number, field: keyof PaymentEntry, value: string | number) => {
    setPayments(prev => prev.map((p, i) => i === idx ? { ...p, [field]: value } : p));
  };

  const handleCancel = async () => {
    if (!sale || !cancelReason.trim()) {
      toast.error('Informe o motivo do cancelamento.');
      return;
    }
    setCancelling(true);
    try {
      const updated = await saleService.cancel(sale.id, { cancellationReason: cancelReason });
      setSale(updated);
      toast.success('Venda cancelada. Estoque devolvido.');
      setShowCancel(false);
      setCancelReason('');
    } catch (err: any) {
      toast.error(err?.response?.data?.detail ?? 'Erro ao cancelar venda.');
    } finally {
      setCancelling(false);
    }
  };

  const handleProcess = async () => {
    if (!sale) return;
    if (payments.some(p => !p.paymentMethodId || Number(p.amount) <= 0)) {
      toast.error('Verifique os valores de pagamento.');
      return;
    }
    if (totalPayments > sale.totalAmount - sale.amountPaid + 0.01) {
      toast.error('Valor total dos pagamentos excede o saldo da venda.');
      return;
    }
    setProcessing(true);
    try {
      const updated = await saleService.processPayment(sale.id, {
        payments: payments.map(p => ({
          paymentMethodId: p.paymentMethodId,
          amount: Number(p.amount),
          installments: Number(p.installments) || 1,
          transactionRef: p.transactionRef || undefined,
        })),
      });
      setSale(updated);
      if (updated.status === 'paid') {
        toast.success('Pagamento confirmado! Venda concluída.');
        navigate('/checkout');
      } else {
        toast.success('Pagamento parcial registrado.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.detail ?? 'Erro ao processar pagamento.');
    } finally {
      setProcessing(false);
    }
  };

  if (loading) return <div className="flex h-64 items-center justify-center text-gray-500 dark:text-gray-400">Carregando...</div>;
  if (!sale) return null;

  const amountDue = sale.totalAmount - sale.amountPaid;
  const isPaid = sale.status === 'paid';
  const isCancelled = sale.status === 'cancelled';

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Checkout</h1>
        <div className="flex items-center gap-3">
          <span className="text-sm text-gray-400 dark:text-gray-500 font-mono">{sale.saleNumber}</span>
          {!isCancelled && (
            <button
              onClick={() => setShowCancel(true)}
              className="rounded border border-red-300 dark:border-red-700 px-3 py-1 text-xs font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20"
            >
              Cancelar venda
            </button>
          )}
        </div>
      </div>

      {/* Sale summary */}
      <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
        <div className="border-b border-gray-200 dark:border-gray-700 px-4 py-3">
          <div className="flex items-center justify-between">
            <span className="font-medium text-gray-900 dark:text-white">
              {sale.customerName || 'Cliente não identificado'}
            </span>
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${
              isPaid
                ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
                : sale.status === 'partially_paid'
                  ? 'bg-yellow-100 dark:bg-yellow-900/40 text-yellow-700 dark:text-yellow-400'
                  : 'bg-brand-100 dark:bg-brand-900/40 text-brand-700 dark:text-brand-400'
            }`}>
              {isPaid ? 'Pago' : sale.status === 'partially_paid' ? 'Parcial' : 'Pendente'}
            </span>
          </div>
        </div>
        <div className="px-4 py-3">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs uppercase text-gray-400 dark:text-gray-500">
                <th className="pb-2 text-left">Item</th>
                <th className="pb-2 text-center">Qtd</th>
                <th className="pb-2 text-right">Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50 dark:divide-gray-800">
              {sale.items.map(item => (
                <tr key={item.id}>
                  <td className="py-1.5 text-gray-800 dark:text-gray-200">{item.productName}</td>
                  <td className="py-1.5 text-center text-gray-500 dark:text-gray-400">{item.quantity}</td>
                  <td className="py-1.5 text-right text-gray-800 dark:text-gray-200">R$ {item.lineTotal.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="border-t border-gray-200 dark:border-gray-700 px-4 py-3 space-y-1">
          {sale.discountAmount > 0 && (
            <div className="flex justify-between text-sm text-red-600 dark:text-red-400">
              <span>Desconto</span>
              <span>- R$ {sale.discountAmount.toFixed(2)}</span>
            </div>
          )}
          <div className="flex justify-between font-bold text-gray-900 dark:text-white">
            <span>Total</span>
            <span className="text-brand-700 dark:text-brand-400">R$ {sale.totalAmount.toFixed(2)}</span>
          </div>
          {sale.amountPaid > 0 && (
            <div className="flex justify-between text-sm text-green-700 dark:text-green-400">
              <span>Já pago</span>
              <span>R$ {sale.amountPaid.toFixed(2)}</span>
            </div>
          )}
          <div className="flex justify-between font-semibold text-orange-700 dark:text-orange-400">
            <span>Saldo devedor</span>
            <span>R$ {amountDue.toFixed(2)}</span>
          </div>
        </div>
      </div>

      {/* Payment form */}
      {!isPaid && !isCancelled && (
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 space-y-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">Formas de pagamento</h2>

          {payments.map((p, idx) => {
            const method = paymentMethods.find(m => m.id === p.paymentMethodId);
            return (
              <div key={idx} className="flex gap-2 items-start">
                <div className="flex-1 space-y-2">
                  <select
                    value={p.paymentMethodId}
                    onChange={e => updatePayment(idx, 'paymentMethodId', e.target.value)}
                    className={fieldCls}
                  >
                    <option value="">Selecione...</option>
                    {paymentMethods.map(m => (
                      <option key={m.id} value={m.id}>{m.name}</option>
                    ))}
                  </select>
                  <div className="flex gap-2">
                    <div className="flex-1">
                      <label className="text-xs text-gray-400 dark:text-gray-500">Valor</label>
                      <CurrencyInput
                        value={p.amount}
                        onValueChange={v => updatePayment(idx, 'amount', v)}
                        className={fieldCls}
                      />
                    </div>
                    {method?.requiresInstallments && (
                      <div className="w-24">
                        <label className="text-xs text-gray-400 dark:text-gray-500">Parcelas</label>
                        <select
                          value={p.installments}
                          onChange={e => updatePayment(idx, 'installments', Number(e.target.value))}
                          className={fieldCls}
                        >
                          {Array.from({ length: method.maxInstallments }, (_, i) => i + 1).map(n => (
                            <option key={n} value={n}>{n}x</option>
                          ))}
                        </select>
                      </div>
                    )}
                  </div>
                  <input
                    type="text"
                    value={p.transactionRef}
                    onChange={e => updatePayment(idx, 'transactionRef', e.target.value)}
                    placeholder="NSU / TxID / Cód. autorização (opcional)"
                    className={fieldCls}
                  />
                </div>
                {payments.length > 1 && (
                  <button onClick={() => removePaymentRow(idx)} className="mt-1 text-red-400 hover:text-red-600">✕</button>
                )}
              </div>
            );
          })}

          <div className="flex items-center justify-between">
            <button onClick={addPaymentRow} className="text-sm text-brand-600 dark:text-brand-400 hover:underline">
              + Adicionar forma de pagamento
            </button>
            <div className="text-sm font-medium">
              <span className={
                remaining < -0.01 ? 'text-red-600 dark:text-red-400'
                : remaining > 0.01 ? 'text-orange-600 dark:text-orange-400'
                : 'text-green-700 dark:text-green-400'
              }>
                {remaining < -0.01 ? `Excesso: R$ ${Math.abs(remaining).toFixed(2)}`
                  : remaining > 0.01 ? `Faltam: R$ ${remaining.toFixed(2)}`
                  : 'Valor OK ✓'}
              </span>
            </div>
          </div>

          <button
            onClick={handleProcess}
            disabled={processing || payments.some(p => !p.paymentMethodId || Number(p.amount) <= 0)}
            className="w-full rounded-md bg-green-600 py-3 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-40"
          >
            {processing ? 'Processando...' : 'Confirmar pagamento'}
          </button>
        </div>
      )}

      {isPaid && (
        <div className="rounded-lg border border-green-200 dark:border-green-800 bg-green-50 dark:bg-green-900/20 p-6 text-center space-y-2">
          <div className="text-2xl">✓</div>
          <div className="font-semibold text-green-800 dark:text-green-400">Venda concluída!</div>
          <div className="text-sm text-green-600 dark:text-green-500">
            Total pago: R$ {sale.amountPaid.toFixed(2)}
          </div>
          <button
            onClick={() => navigate('/checkout')}
            className="mt-2 rounded-md bg-green-700 dark:bg-green-600 px-6 py-2 text-sm font-medium text-white hover:bg-green-800 dark:hover:bg-green-700"
          >
            Voltar ao caixa
          </button>
        </div>
      )}

      {isCancelled && (
        <div className="rounded-lg border border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/20 p-6 text-center space-y-2">
          <div className="font-semibold text-red-800 dark:text-red-400">Venda cancelada</div>
          {sale.cancellationReason && (
            <div className="text-sm text-red-600 dark:text-red-500">
              Motivo: {sale.cancellationReason}
            </div>
          )}
          <div className="text-xs text-red-500 dark:text-red-400">
            Estoque devolvido{sale.payments.some(p => p.status === 'refunded') && ', pagamentos estornados'}.
          </div>
          <button
            onClick={() => navigate('/checkout')}
            className="mt-2 rounded-md bg-gray-700 dark:bg-gray-600 px-6 py-2 text-sm font-medium text-white hover:bg-gray-800 dark:hover:bg-gray-700"
          >
            Voltar ao caixa
          </button>
        </div>
      )}

      {showCancel && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white dark:bg-gray-900 p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-semibold text-red-700 dark:text-red-400">Cancelar venda</h3>
            {sale.amountPaid > 0 && (
              <div className="rounded border border-orange-200 dark:border-orange-800 bg-orange-50 dark:bg-orange-900/20 p-3 text-sm text-orange-800 dark:text-orange-300">
                Esta venda tem R$ {sale.amountPaid.toFixed(2)} em pagamentos. Eles serão marcados como estornados.
              </div>
            )}
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Motivo *</label>
              <textarea
                value={cancelReason}
                onChange={e => setCancelReason(e.target.value)}
                rows={3}
                className={fieldCls}
                placeholder="Informe o motivo do cancelamento..."
              />
            </div>
            <div className="flex gap-2 justify-end">
              <button
                onClick={() => { setShowCancel(false); setCancelReason(''); }}
                className="rounded border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                Voltar
              </button>
              <button
                onClick={handleCancel}
                disabled={cancelling || !cancelReason.trim()}
                className="rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                {cancelling ? 'Cancelando...' : 'Confirmar cancelamento'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
