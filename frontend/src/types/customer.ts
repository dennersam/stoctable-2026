export interface Customer {
  id: string;
  fullName: string;
  documentType: 'CPF' | 'CNPJ';
  documentNumber?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  address?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  customerTypeId?: string;
  customerTypeName?: string;
  creditLimit: number;
  isActive: boolean;
  notes?: string;
}

export interface CustomerCrmNote {
  id: string;
  customerId: string;
  note: string;
  noteType: 'general' | 'complaint' | 'followup';
  createdAt: string;
  createdBy: string;
}

export interface CreateCustomerRequest {
  fullName: string;
  documentType: 'CPF' | 'CNPJ';
  documentNumber?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  address?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  customerTypeId?: string;
  creditLimit?: number;
  notes?: string;
}
