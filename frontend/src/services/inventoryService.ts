import api from '@/lib/api';
import type { InventoryMovement, AdjustStockRequest } from '@/types/inventory';

export const inventoryService = {
  getMovements: async (productId: string) => {
    const response = await api.get<InventoryMovement[]>(`/inventory/movements/${productId}`);
    return response.data;
  },

  adjustStock: async (data: AdjustStockRequest) => {
    const response = await api.post<InventoryMovement>('/inventory/adjust', data);
    return response.data;
  },
};
