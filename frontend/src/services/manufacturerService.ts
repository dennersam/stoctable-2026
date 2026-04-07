import api from '@/lib/api';
import type { Manufacturer, CreateManufacturerRequest } from '@/types/manufacturer';
import type { PaginatedResult } from '@/types/common';

export const manufacturerService = {
  // Retorna apenas ativos — para uso em selects
  getActive: async () => {
    const response = await api.get<Manufacturer[]>('/manufacturers');
    return response.data;
  },

  // Lista completa paginada (admin)
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<PaginatedResult<Manufacturer>>('/manufacturers/all', { params });
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

  hardDelete: async (id: string) => {
    await api.delete(`/manufacturers/${id}/permanent`);
  },
};
