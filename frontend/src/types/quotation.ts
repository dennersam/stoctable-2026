export type QuotationStatus = 'draft' | 'finalized' | 'converted' | 'cancelled';

export interface QuotationItem {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  lineTotal: number;
}

export interface Quotation {
  id: string;
  quotationNumber: string;
  customerId?: string;
  customerName?: string;
  salespersonId: string;
  salespersonName: string;
  status: QuotationStatus;
  subtotal: number;
  discountPct: number;
  discountAmount: number;
  totalAmount: number;
  notes?: string;
  validUntil?: string;
  finalizedAt?: string;
  cancelledAt?: string;
  cancellationReason?: string;
  convertedToSaleId?: string;
  items: QuotationItem[];
  createdAt: string;
}

export interface CreateQuotationRequest {
  customerId?: string;
  notes?: string;
  validUntil?: string;
}

export interface AddQuotationItemRequest {
  productId: string;
  quantity: number;
  discountPct?: number;
}

export interface FinalizeQuotationRequest {
  discountPct?: number;
  notes?: string;
}

export interface CancelQuotationRequest {
  cancellationReason: string;
}
