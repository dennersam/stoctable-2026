export type SaleStatus = 'pending_payment' | 'partially_paid' | 'paid' | 'cancelled';
export type PaymentStatus = 'pending' | 'completed' | 'refunded';

export interface SaleItem {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  lineTotal: number;
}

export interface Payment {
  id: string;
  saleId: string;
  paymentMethodId: string;
  paymentMethodName: string;
  amount: number;
  installments: number;
  status: PaymentStatus;
  transactionRef?: string;
  paidAt?: string;
}

export interface PaymentMethod {
  id: string;
  name: string;
  requiresInstallments: boolean;
  maxInstallments: number;
  isActive: boolean;
}

export interface Sale {
  id: string;
  saleNumber: string;
  quotationId?: string;
  customerId?: string;
  customerName?: string;
  salespersonId: string;
  salespersonName: string;
  cashierId?: string;
  cashierName?: string;
  status: SaleStatus;
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  amountPaid: number;
  notes?: string;
  completedAt?: string;
  items: SaleItem[];
  payments: Payment[];
  createdAt: string;
}

export interface ProcessPaymentRequest {
  payments: {
    paymentMethodId: string;
    amount: number;
    installments?: number;
    transactionRef?: string;
  }[];
}
