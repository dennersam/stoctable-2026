import api from '@/lib/api';

export interface Supplier {
  id: string;
  companyName: string;
  tradeName?: string;
  cnpj?: string;
  address?: string;
  phone?: string;
  email?: string;
  contactPerson?: string;
  isActive: boolean;
  notes?: string;
  createdAt: string;
}

export type SupplierOption = Pick<Supplier, 'id' | 'companyName' | 'tradeName'>;

export interface CreateSupplierRequest {
  companyName: string;
  tradeName?: string;
  cnpj?: string;
  address?: string;
  phone?: string;
  email?: string;
  contactPerson?: string;
  notes?: string;
}

export const supplierService = {
  getAll: async () => {
    const response = await api.get<Supplier[]>('/suppliers');
    return response.data;
  },

  search: async (q: string) => {
    const response = await api.get<Supplier[]>('/suppliers/search', { params: { q } });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await api.get<Supplier>(`/suppliers/${id}`);
    return response.data;
  },

  create: async (data: CreateSupplierRequest) => {
    const response = await api.post<Supplier>('/suppliers', data);
    return response.data;
  },

  update: async (id: string, data: CreateSupplierRequest) => {
    const response = await api.put<Supplier>(`/suppliers/${id}`, data);
    return response.data;
  },

  deactivate: async (id: string) => {
    await api.delete(`/suppliers/${id}`);
  },
};
