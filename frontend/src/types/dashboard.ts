export interface SalesSummary {
  revenue: number;
  count: number;
  averageTicket: number;
}

export interface DailySales {
  /** Data do calendário da loja (yyyy-MM-dd), sem fuso. */
  date: string;
  revenue: number;
  count: number;
}

export interface OpenQuotations {
  count: number;
  amount: number;
}

export interface PaymentMethodTotal {
  method: string;
  amount: number;
  count: number;
}

export interface PendingSale {
  saleId: string;
  saleNumber: string;
  customerName: string | null;
  amountRemaining: number;
  createdAt: string;
}

export interface PendingSales {
  count: number;
  amountRemaining: number;
  oldest: PendingSale[];
}

export interface TopProduct {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  revenue: number;
}

export interface TopSalesperson {
  name: string;
  revenue: number;
  count: number;
}

export interface Margin {
  revenue: number;
  profit: number;
  percent: number;
}

export interface LowStockItem {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  minimum: number;
}

export interface StockHealth {
  lowCount: number;
  outCount: number;
  deadCount: number;
  deadValue: number;
  lowItems: LowStockItem[];
}

export interface SalespersonDashboard {
  today: SalesSummary;
  month: SalesSummary;
  previousMonthToDate: SalesSummary;
  dailySales: DailySales[];
  openQuotations: OpenQuotations;
}

export interface CashierDashboard {
  receivedToday: number;
  paymentsToday: number;
  byMethod: PaymentMethodTotal[];
  pendingSales: PendingSales;
  refundsToday: { count: number; amount: number };
}

export interface AdminDashboard {
  today: SalesSummary;
  month: SalesSummary;
  previousMonthToDate: SalesSummary;
  dailySales: DailySales[];
  margin: Margin;
  topProducts: TopProduct[];
  topSalespeople: TopSalesperson[];
  byMethod: PaymentMethodTotal[];
  openQuotations: OpenQuotations;
  pendingSales: PendingSales;
  stock: StockHealth;
}
