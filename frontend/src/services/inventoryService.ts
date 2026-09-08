import api from '@/lib/api';
import type { InventoryMovement, AdjustStockRequest } from '@/types/inventory';
import type { BranchStock } from '@/types/stockTransfer';

export const inventoryService = {
  getMovements: async (productId: string) => {
    const response = await api.get<InventoryMovement[]>(`/inventory/movements/${productId}`);
    return response.data;
  },

  adjustStock: async (data: AdjustStockRequest) => {
    const response = await api.post<InventoryMovement>('/inventory/adjust', data);
    return response.data;
  },

  /** Saldo do produto em todas as lojas que a conta enxerga. Só leitura. */
  getNetworkStock: async (productId: string) => {
    const response = await api.get<BranchStock[]>(`/inventory/network/${productId}`);
    return response.data;
  },

  /** O mínimo é da filial ativa, não do catálogo. */
  setMinimum: async (productId: string, minimum: number) => {
    await api.put('/inventory/minimum', { productId, minimum });
  },
};
