export interface Driver {
  id: number;
  fullName: string;
  phone: string;
  licenseNumber: string;
  licenseExpiryDate: string;
  cnic?: string;
  address?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateDriverRequest {
  fullName: string;
  phone: string;
  licenseNumber: string;
  licenseExpiryDate: string;
  cnic?: string;
  address?: string;
}

export interface UpdateDriverRequest extends CreateDriverRequest {
  id: number;
}
