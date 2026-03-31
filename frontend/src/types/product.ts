export interface Product {
  id: string;
  sku: string;
  barcode?: string;
  name: string;
  manufacturerId?: string;
  manufacturerName?: string;
  categoryId?: string;
  categoryName?: string;
  supplierId?: string;
  supplierName?: string;
  costPrice: number;
  salePrice: number;
  stockQuantity: number;
  stockReserved: number;
  stockMinimum: number;
  unit: string;
  icmsRate?: number;
  ipiRate?: number;
  cst?: string;
  ncm?: string;
  imageUrl?: string;
  isActive: boolean;
  notes?: string;
}

export interface ProductCategory {
  id: string;
  name: string;
  parentId?: string;
  isActive: boolean;
}

export interface CreateProductRequest {
  barcode?: string;
  name: string;
  manufacturerId?: string;
  categoryId?: string;
  supplierId?: string;
  costPrice: number;
  salePrice: number;
  stockMinimum: number;
  unit: string;
  icmsRate?: number;
  ipiRate?: number;
  cst?: string;
  ncm?: string;
  notes?: string;
}

export type UpdateProductRequest = Partial<CreateProductRequest>;
