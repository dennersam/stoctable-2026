import api from '@/lib/api';
import type {
  StockTransfer,
  StockTransferStatus,
  CreateStockTransferRequest,
  ReceiveStockTransferRequest,
} from '@/types/stockTransfer';

/**
 * A filial de origem nunca é enviada: ela é a filial da sessão, que o backend
 * lê da claim assinada. Só o destino trafega.
 */
export const stockTransferService = {
  list: async (direction: 'outbound' | 'inbound', status?: StockTransferStatus) => {
    const response = await api.get<StockTransfer[]>('/stock-transfers', {
      params: { direction, status },
    });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await api.get<StockTransfer>(`/stock-transfers/${id}`);
    return response.data;
  },

  create: async (data: CreateStockTransferRequest) => {
    const response = await api.post<StockTransfer>('/stock-transfers', data);
    return response.data;
  },

  ship: async (id: string) => {
    const response = await api.post<StockTransfer>(`/stock-transfers/${id}/ship`);
    return response.data;
  },

  receive: async (id: string, data: ReceiveStockTransferRequest) => {
    const response = await api.post<StockTransfer>(`/stock-transfers/${id}/receive`, data);
    return response.data;
  },

  cancel: async (id: string, cancellationReason: string) => {
    const response = await api.post<StockTransfer>(`/stock-transfers/${id}/cancel`, {
      cancellationReason,
    });
    return response.data;
  },
};
