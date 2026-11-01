export function formatQuantity(value: number) {
  return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value);
}

export function salesCount(count: number) {
  return `${formatQuantity(count)} ${count === 1 ? 'venda' : 'vendas'}`;
}

export function paymentsCount(count: number) {
  return `${formatQuantity(count)} ${count === 1 ? 'pagamento' : 'pagamentos'}`;
}
