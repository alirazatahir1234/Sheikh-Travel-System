export interface Customer {
  id: number;
  fullName: string;
  phone: string;
  email?: string | null;
  address?: string | null;
  cnic?: string | null;
  fatherOrHusbandName?: string | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  nationality?: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface CreateCustomerDto {
  fullName: string;
  phone: string;
  email?: string | null;
  address?: string | null;
  cnic?: string | null;
  fatherOrHusbandName?: string | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  nationality?: string | null;
}

export interface UpdateCustomerDto extends CreateCustomerDto {}

export interface CreateCustomerRequest {
  customer: CreateCustomerDto;
}

export interface UpdateCustomerRequest {
  id: number;
  customer: UpdateCustomerDto;
}

export interface CustomerFilter {
  search?: string;
  isActive?: boolean;
  recency?: string;
}

export interface CustomerListStats {
  total: number;
  newCount: number;
  returning: number;
}
