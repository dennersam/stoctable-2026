import api from '@/lib/api';
import type { Customer, CustomerCrmNote, CreateCustomerRequest } from '@/types/customer';
import type { PaginatedResult } from '@/types/common';

export const customerService = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<PaginatedResult<Customer>>('/customers', { params });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await api.get<Customer>(`/customers/${id}`);
    return response.data;
  },

  create: async (data: CreateCustomerRequest) => {
    const response = await api.post<Customer>('/customers', data);
    return response.data;
  },

  update: async (id: string, data: Partial<CreateCustomerRequest>) => {
    const response = await api.put<Customer>(`/customers/${id}`, data);
    return response.data;
  },

  getCrmNotes: async (customerId: string) => {
    const response = await api.get<CustomerCrmNote[]>(`/customers/${customerId}/crm-notes`);
    return response.data;
  },

  addCrmNote: async (customerId: string, note: string, noteType: CustomerCrmNote['noteType'] = 'general') => {
    const response = await api.post<CustomerCrmNote>(`/customers/${customerId}/crm-notes`, { note, noteType });
    return response.data;
  },

  deactivate: async (id: string) => {
    await api.delete(`/customers/${id}`);
  },

  hardDelete: async (id: string) => {
    await api.delete(`/customers/${id}/permanent`);
  },
};
