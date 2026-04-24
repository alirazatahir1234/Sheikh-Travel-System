export interface Customer {
  id: number;
  fullName: string;
  phone: string;
  email?: string | null;
  address?: string | null;
  cnic?: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface CreateCustomerDto {
  fullName: string;
  phone: string;
  email?: string | null;
  address?: string | null;
  cnic?: string | null;
}

export interface UpdateCustomerDto extends CreateCustomerDto {}

export interface CreateCustomerRequest {
  customer: CreateCustomerDto;
}

export interface UpdateCustomerRequest {
  id: number;
  customer: UpdateCustomerDto;
}
