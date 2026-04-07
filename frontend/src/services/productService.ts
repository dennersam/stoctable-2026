import api from '@/lib/api';
import type { Product, CreateProductRequest, UpdateProductRequest } from '@/types/product';
import type { PaginatedResult } from '@/types/common';

export const productService = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string; categoryId?: string }) => {
    const response = await api.get<PaginatedResult<Product>>('/products', { params });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await api.get<Product>(`/products/${id}`);
    return response.data;
  },

  search: async (query: string) => {
    const response = await api.get<Product[]>('/products/search', { params: { q: query } });
    return response.data;
  },

  create: async (data: CreateProductRequest) => {
    const response = await api.post<Product>('/products', data);
    return response.data;
  },

  update: async (id: string, data: UpdateProductRequest) => {
    const response = await api.put<Product>(`/products/${id}`, data);
    return response.data;
  },

  delete: async (id: string) => {
    await api.delete(`/products/${id}`);
  },

  hardDelete: async (id: string) => {
    await api.delete(`/products/${id}/permanent`);
  },

  uploadImage: async (id: string, file: File) => {
    const formData = new FormData();
    formData.append('image', file);
    const response = await api.post<{ imageUrl: string }>(`/products/${id}/image`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  },
};
