import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { CheckCircle2, Plus, X } from 'lucide-react';
import { saleService } from '@/services/saleService';
import type { Sale, PaymentMethod } from '@/types/sale';
import { Button } from '@/components/ui/Button';
import { CurrencyInput } from '@/components/ui/CurrencyInput';
import { Field } from '@/components/ui/Field';
import { fieldClass } from '@/components/ui/fieldClass';
import { Modal, ModalBody, ModalFooter } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHeader';
import { cn, formatCurrency } from '@/lib/utils';

interface PaymentEntry {
  paymentMethodId: string;
  amount: number;
  installments: number;
  transactionRef: string;
}

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

  if (loading) return <div role="status" className="flex h-64 items-center justify-center text-sm text-gray-500 dark:text-gray-400">Carregando...</div>;
  if (!sale) return null;

  const amountDue = sale.totalAmount - sale.amountPaid;
  const isPaid = sale.status === 'paid';
  const isCancelled = sale.status === 'cancelled';
  const isOpen = !isPaid && !isCancelled;

  return (
    <div className="mx-auto flex min-h-full max-w-2xl flex-col gap-4">
      <PageHeader title="Caixa" subtitle={<span className="font-mono">{sale.saleNumber}</span>}>
        {!isCancelled && (
          <Button variant="danger-outline" size="sm" onClick={() => setShowCancel(true)}>Cancelar venda</Button>
        )}
      </PageHeader>

      {/* Sale summary */}
      <section className="rounded-lg border border-line bg-surface">
        <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
          <span className="min-w-0 font-medium text-gray-900 dark:text-white">
            {sale.customerName || 'Cliente não identificado'}
          </span>
          <span className={cn(
            'shrink-0 rounded-full px-2 py-0.5 text-xs font-medium',
            isPaid
              ? 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-400'
              : sale.status === 'partially_paid'
                ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-400'
                : 'bg-gray-100 text-gray-700 dark:bg-white/10 dark:text-gray-200'
          )}>
            {isPaid ? 'Pago' : sale.status === 'partially_paid' ? 'Parcial' : 'Pendente'}
          </span>
        </div>

        <ul className="divide-y divide-gray-100 px-4 text-sm dark:divide-white/5">
          {sale.items.map(item => (
            <li key={item.id} className="flex items-baseline gap-3 py-2">
              <span className="w-8 shrink-0 tabular-nums text-gray-500 dark:text-gray-400">{item.quantity}×</span>
              <span className="min-w-0 flex-1 text-gray-800 dark:text-gray-200">{item.productName}</span>
              <span className="shrink-0 tabular-nums text-gray-800 dark:text-gray-200">{formatCurrency(item.lineTotal)}</span>
            </li>
          ))}
        </ul>

        <dl className="space-y-1 border-t border-line px-4 py-3 text-sm tabular-nums">
          {sale.discountAmount > 0 && (
            <div className="flex justify-between text-gray-600 dark:text-gray-300">
              <dt>Desconto</dt>
              <dd>- {formatCurrency(sale.discountAmount)}</dd>
            </div>
          )}
          <div className="flex items-baseline justify-between text-gray-900 dark:text-white">
            <dt className="font-medium">Total</dt>
            <dd className="text-xl font-bold">{formatCurrency(sale.totalAmount)}</dd>
          </div>
          {/* Só existe saldo diferente do total depois de um pagamento parcial. */}
          {sale.amountPaid > 0 && (
            <>
              <div className="flex justify-between text-green-800 dark:text-green-400">
                <dt>Já pago</dt>
                <dd>{formatCurrency(sale.amountPaid)}</dd>
              </div>
              <div className="flex justify-between font-semibold text-gray-900 dark:text-white">
                <dt>Saldo a receber</dt>
                <dd>{formatCurrency(amountDue)}</dd>
              </div>
            </>
          )}
        </dl>
      </section>

      {/* Payment form */}
      {isOpen && (
        <section className="space-y-4 rounded-lg border border-line bg-surface p-4">
          <h2 className="font-semibold text-gray-900 dark:text-white">Forma de pagamento</h2>

          {payments.map((p, idx) => {
            const method = paymentMethods.find(m => m.id === p.paymentMethodId);
            return (
              <div key={idx} className={cn('space-y-3', idx > 0 && 'border-t border-line pt-4')}>
                <div className="flex items-start gap-2">
                  <div role="radiogroup" aria-label="Forma de pagamento" className="grid flex-1 grid-cols-2 gap-2 sm:grid-cols-3">
                    {paymentMethods.map(m => (
                      <button
                        key={m.id}
                        type="button"
                        role="radio"
                        aria-checked={p.paymentMethodId === m.id}
                        onClick={() => updatePayment(idx, 'paymentMethodId', m.id)}
                        className={cn(
                          'min-h-11 rounded-md border px-2 py-2 text-sm font-medium transition-colors',
                          p.paymentMethodId === m.id
                            ? 'border-brand-600 bg-brand-50 text-brand-800 dark:border-brand-300 dark:bg-white/10 dark:text-brand-200'
                            : 'border-gray-300 text-gray-700 hover:bg-gray-50 dark:border-white/15 dark:text-gray-300 dark:hover:bg-white/5'
                        )}
                      >
                        {m.name}
                      </button>
                    ))}
                  </div>
                  {payments.length > 1 && (
                    <button
                      type="button"
                      aria-label="Remover esta forma de pagamento"
                      onClick={() => removePaymentRow(idx)}
                      className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md text-gray-500 hover:bg-red-50 hover:text-red-700 dark:text-gray-400 dark:hover:bg-red-900/30 dark:hover:text-red-300"
                    >
                      <X size={16} />
                    </button>
                  )}
                </div>

                <div className="flex gap-2">
                  <Field label="Valor" className="flex-1">
                    <CurrencyInput
                      value={p.amount}
                      onValueChange={v => updatePayment(idx, 'amount', v)}
                      className={fieldClass}
                    />
                  </Field>
                  {method?.requiresInstallments && (
                    <Field label="Parcelas" className="w-24">
                      <select
                        value={p.installments}
                        onChange={e => updatePayment(idx, 'installments', Number(e.target.value))}
                        className={fieldClass}
                      >
                        {Array.from({ length: method.maxInstallments }, (_, i) => i + 1).map(n => (
                          <option key={n} value={n}>{n}x</option>
                        ))}
                      </select>
                    </Field>
                  )}
                </div>

                <Field label="NSU, TxID ou autorização (opcional)">
                  <input
                    type="text"
                    value={p.transactionRef}
                    onChange={e => updatePayment(idx, 'transactionRef', e.target.value)}
                    className={fieldClass}
                  />
                </Field>
              </div>
            );
          })}

          <Button variant="ghost" size="sm" onClick={addPaymentRow} className="-ml-3">
            <Plus size={16} />
            Dividir em outra forma de pagamento
          </Button>
        </section>
      )}

      {/* Confirmação: fica presa ao rodapé no celular, para não rolar até o fim a cada venda */}
      {isOpen && (
        <div className="sticky -bottom-4 -mx-4 -mb-4 mt-auto flex flex-col gap-2 border-t border-line bg-surface px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] md:static md:m-0 md:rounded-lg md:border md:p-4">
          <p className={cn(
            'text-center text-sm font-medium tabular-nums',
            remaining < -0.01 ? 'text-red-700 dark:text-red-400'
            : remaining > 0.01 ? 'text-amber-800 dark:text-amber-400'
            : 'text-green-800 dark:text-green-400'
          )}>
            {remaining < -0.01 ? `Excesso de ${formatCurrency(Math.abs(remaining))}`
              : remaining > 0.01 ? `Faltam ${formatCurrency(remaining)} para quitar`
              : `Recebendo ${formatCurrency(totalPayments)}`}
          </p>
          <Button
            variant="success"
            size="lg"
            className="w-full"
            onClick={handleProcess}
            disabled={processing || payments.some(p => !p.paymentMethodId || Number(p.amount) <= 0)}
          >
            {processing ? 'Processando...' : 'Confirmar pagamento'}
          </Button>
        </div>
      )}

      {isPaid && (
        <div className="space-y-2 rounded-lg border border-green-200 bg-green-50 p-6 text-center dark:border-green-800 dark:bg-green-900/20">
          <CheckCircle2 size={28} className="mx-auto text-green-700 dark:text-green-400" />
          <div className="font-semibold text-green-800 dark:text-green-400">Venda concluída</div>
          <div className="text-sm tabular-nums text-green-800 dark:text-green-400">
            Total pago: {formatCurrency(sale.amountPaid)}
          </div>
          <Button variant="success" className="mt-2" onClick={() => navigate('/checkout')}>Voltar ao caixa</Button>
        </div>
      )}

      {isCancelled && (
        <div className="space-y-2 rounded-lg border border-red-200 bg-red-50 p-6 text-center dark:border-red-800 dark:bg-red-900/20">
          <div className="font-semibold text-red-800 dark:text-red-400">Venda cancelada</div>
          {sale.cancellationReason && (
            <div className="text-sm text-red-700 dark:text-red-400">
              Motivo: {sale.cancellationReason}
            </div>
          )}
          <div className="text-xs text-red-700 dark:text-red-400">
            Estoque devolvido{sale.payments.some(p => p.status === 'refunded') && ', pagamentos estornados'}.
          </div>
          <Button variant="outline" className="mt-2" onClick={() => navigate('/checkout')}>Voltar ao caixa</Button>
        </div>
      )}

      {showCancel && (
        <Modal title="Cancelar venda" onClose={() => { setShowCancel(false); setCancelReason(''); }}>
          <ModalBody className="space-y-4">
            {sale.amountPaid > 0 && (
              <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-900/20 dark:text-amber-300">
                Esta venda tem {formatCurrency(sale.amountPaid)} em pagamentos. Eles serão marcados como estornados.
              </p>
            )}
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
            <Button variant="outline" onClick={() => { setShowCancel(false); setCancelReason(''); }}>Voltar</Button>
            <Button variant="danger" onClick={handleCancel} disabled={cancelling || !cancelReason.trim()}>
              {cancelling ? 'Cancelando...' : 'Cancelar venda'}
            </Button>
          </ModalFooter>
        </Modal>
      )}
    </div>
  );
}
