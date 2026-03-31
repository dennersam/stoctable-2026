export interface InventoryMovement {
  id: string;
  productId: string;
  productName?: string;
  productSku?: string;
  movementType: string;
  quantity: number;
  quantityBefore: number;
  quantityAfter: number;
  referenceType: string;
  referenceId?: string;
  notes?: string;
  createdBy: string;
  createdAt: string;
}

export interface AdjustStockRequest {
  productId: string;
  quantity: number;
  notes: string;
}
