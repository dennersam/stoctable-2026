import api from '@/lib/api';
import type { Manufacturer, CreateManufacturerRequest } from '@/types/manufacturer';

export const manufacturerService = {
  // Retorna apenas ativos — para uso em selects
  getActive: async () => {
    const response = await api.get<Manufacturer[]>('/manufacturers');
    return response.data;
  },

  // Lista completa (admin)
  getAll: async () => {
    const response = await api.get<Manufacturer[]>('/manufacturers/all');
    return response.data;
  },

  create: async (data: CreateManufacturerRequest) => {
    const response = await api.post<Manufacturer>('/manufacturers', data);
    return response.data;
  },

  update: async (id: string, data: CreateManufacturerRequest) => {
    const response = await api.put<Manufacturer>(`/manufacturers/${id}`, data);
    return response.data;
  },

  deactivate: async (id: string) => {
    await api.delete(`/manufacturers/${id}`);
  },
};
