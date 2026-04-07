import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { saleService } from '@/services/saleService';
import type { Sale, PaymentMethod } from '@/types/sale';

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

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Checkout</h1>
        <span className="text-sm text-gray-400 dark:text-gray-500 font-mono">{sale.saleNumber}</span>
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
      {!isPaid && (
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
                      <label className="text-xs text-gray-400 dark:text-gray-500">Valor (R$)</label>
                      <input
                        type="number"
                        min={0.01}
                        step={0.01}
                        value={p.amount}
                        onChange={e => updatePayment(idx, 'amount', e.target.value)}
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
    </div>
  );
}
