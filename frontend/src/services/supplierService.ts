import api from '@/lib/api';

export interface SupplierOption {
  id: string;
  companyName: string;
  tradeName?: string;
  isActive: boolean;
}

export const supplierService = {
  getAll: async () => {
    const response = await api.get<SupplierOption[]>('/suppliers');
    return response.data;
  },

  search: async (q: string) => {
    const response = await api.get<SupplierOption[]>('/suppliers/search', { params: { q } });
    return response.data;
  },
};
