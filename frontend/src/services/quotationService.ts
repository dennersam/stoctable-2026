import api from '@/lib/api';
import type {
  Quotation,
  CreateQuotationRequest,
  AddQuotationItemRequest,
  FinalizeQuotationRequest,
  CancelQuotationRequest,
} from '@/types/quotation';
import type { PaginatedResult } from '@/types/common';

export const quotationService = {
  getAll: async (params?: { page?: number; pageSize?: number; status?: string }) => {
    const response = await api.get<PaginatedResult<Quotation>>('/quotations', { params });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await api.get<Quotation>(`/quotations/${id}`);
    return response.data;
  },

  create: async (data: CreateQuotationRequest) => {
    const response = await api.post<Quotation>('/quotations', data);
    return response.data;
  },

  addItem: async (quotationId: string, data: AddQuotationItemRequest) => {
    const response = await api.post<Quotation>(`/quotations/${quotationId}/items`, data);
    return response.data;
  },

  removeItem: async (quotationId: string, itemId: string) => {
    const response = await api.delete<Quotation>(`/quotations/${quotationId}/items/${itemId}`);
    return response.data;
  },

  finalize: async (quotationId: string, data: FinalizeQuotationRequest) => {
    const response = await api.post<Quotation>(`/quotations/${quotationId}/finalize`, data);
    return response.data;
  },

  cancel: async (quotationId: string, data: CancelQuotationRequest) => {
    const response = await api.post<Quotation>(`/quotations/${quotationId}/cancel`, data);
    return response.data;
  },

  convertToSale: async (quotationId: string) => {
    const response = await api.post<{ saleId: string }>(`/quotations/${quotationId}/convert`);
    return response.data;
  },
};
