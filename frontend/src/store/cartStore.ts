import { create } from 'zustand';
import type { QuotationItem } from '@/types/quotation';

interface CartState {
  quotationId: string | null;
  items: QuotationItem[];
  setQuotationId: (id: string | null) => void;
  setItems: (items: QuotationItem[]) => void;
  clearCart: () => void;
}

export const useCartStore = create<CartState>((set) => ({
  quotationId: null,
  items: [],

  setQuotationId: (id) => set({ quotationId: id }),
  setItems: (items) => set({ items }),
  clearCart: () => set({ quotationId: null, items: [] }),
}));
