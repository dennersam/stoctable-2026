import api from '@/lib/api';
import type { AdminDashboard, CashierDashboard, SalespersonDashboard } from '@/types/dashboard';

export const dashboardService = {
  getSalesperson: async () => {
    const response = await api.get<SalespersonDashboard>('/dashboard/salesperson');
    return response.data;
  },

  getCashier: async () => {
    const response = await api.get<CashierDashboard>('/dashboard/cashier');
    return response.data;
  },

  getAdmin: async () => {
    const response = await api.get<AdminDashboard>('/dashboard/admin');
    return response.data;
  },
};
