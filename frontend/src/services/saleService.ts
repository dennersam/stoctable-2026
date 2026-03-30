import api from '@/lib/api';
import type { Sale, PaymentMethod, ProcessPaymentRequest } from '@/types/sale';
import type { PaginatedResult } from '@/types/common';

export const saleService = {
  getAll: async (params?: { page?: number; pageSize?: number; status?: string }) => {
    const response = await api.get<PaginatedResult<Sale>>('/sales', { params });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await api.get<Sale>(`/sales/${id}`);
    return response.data;
  },

  processPayment: async (saleId: string, data: ProcessPaymentRequest) => {
    const response = await api.post<Sale>(`/sales/${saleId}/payments`, data);
    return response.data;
  },

  getPaymentMethods: async () => {
    const response = await api.get<PaymentMethod[]>('/payment-methods');
    return response.data;
  },
};
