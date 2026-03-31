export interface Manufacturer {
  id: string;
  name: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateManufacturerRequest {
  name: string;
  notes?: string;
}
