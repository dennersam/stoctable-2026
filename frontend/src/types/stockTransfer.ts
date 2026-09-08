export type StockTransferStatus = 'Pending' | 'InTransit' | 'Received' | 'Cancelled';

export interface StockTransferItem {
  productId: string;
  productName?: string;
  sku?: string;
  quantitySent: number;
  /** Null enquanto não conferido — diferente de ter chegado zero. */
  quantityReceived: number | null;
}

export interface StockTransfer {
  id: string;
  transferNumber: string;
  originBranchId: string;
  destinationBranchId: string;
  status: StockTransferStatus;
  hasDivergence: boolean;
  shippedAt?: string;
  shippedBy?: string;
  receivedAt?: string;
  receivedBy?: string;
  cancelledAt?: string;
  cancellationReason?: string;
  notes?: string;
  createdAt: string;
  items: StockTransferItem[];
}

export interface CreateStockTransferRequest {
  destinationBranchId: string;
  items: { productId: string; quantity: number }[];
  notes?: string;
}

export interface ReceiveStockTransferRequest {
  /** Itens omitidos assumem que chegou tudo o que saiu. */
  items?: { productId: string; quantityReceived: number }[];
}

/** Saldo de um produto em uma filial da rede. */
export interface BranchStock {
  branchId: string;
  quantity: number;
  reserved: number;
  available: number;
  minimum: number;
  /** Saiu daqui e ainda não foi recebido — não está nesta loja nem na outra. */
  inTransit: number;
}

export const TRANSFER_STATUS_LABEL: Record<StockTransferStatus, string> = {
  Pending: 'Pendente',
  InTransit: 'Em trânsito',
  Received: 'Recebida',
  Cancelled: 'Cancelada',
};

export const TRANSFER_STATUS_BADGE: Record<StockTransferStatus, string> = {
  Pending: 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300',
  InTransit: 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-400',
  Received: 'bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-400',
  Cancelled: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400',
};
