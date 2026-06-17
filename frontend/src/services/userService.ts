import api from '@/lib/api';
import type { SystemUser, CreateUserRequest, UpdateUserRequest } from '@/types/user';

export const userService = {
  getAll: async () => {
    const response = await api.get<SystemUser[]>('/users');
    return response.data;
  },

  getById: async (id: string) => {
    const response = await api.get<SystemUser>(`/users/${id}`);
    return response.data;
  },

  create: async (data: CreateUserRequest) => {
    const response = await api.post<SystemUser>('/users', data);
    return response.data;
  },

  update: async (id: string, data: UpdateUserRequest) => {
    const response = await api.put<SystemUser>(`/users/${id}`, data);
    return response.data;
  },
};
