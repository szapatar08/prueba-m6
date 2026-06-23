// Auth types
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface LoginResponse {
  token: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

export interface RegisterResponse {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  createdAt: string;
}

export interface UserProfile {
  userId: string;
  email: string;
  tenantId: string;
  roles: string[];
}

// Property types
export interface Property {
  id: string;
  name: string;
  description: string;
  location: string;
  city: string;
  country: string;
  pricePerNight: number;
  maxGuests: number;
  bedrooms: number;
  bathrooms: number;
  primaryImageUrl: string | null;
}

export interface PropertyDetail {
  id: string;
  name: string;
  description: string;
  location: string;
  address: string;
  city: string;
  country: string;
  pricePerNight: number;
  maxGuests: number;
  bedrooms: number;
  bathrooms: number;
  ownerId: string;
  createdAt: string;
  images: PropertyInfo[];
}

export interface PropertyInfo {
  id: string;
  url: string;
  isPrimary: boolean;
}

export interface BrowseCatalogResult {
  properties: Property[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreatePropertyRequest {
  name: string;
  description: string;
  location: string;
  address: string;
  city: string;
  country: string;
  pricePerNight: number;
  maxGuests: number;
  bedrooms: number;
  bathrooms: number;
}

// Booking types
export interface Booking {
  id: string;
  propertyId: string;
  propertyName?: string;
  guestId: string;
  guestEmail?: string;
  startDate: string;
  endDate: string;
  totalPrice: number;
  status: BookingStatus;
  createdAt: string;
}

export type BookingStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled';

export interface CreateBookingRequest {
  propertyId: string;
  startDate: string;
  endDate: string;
  totalPrice: number;
}

export interface AvailabilityCheck {
  propertyId: string;
  startDate: string;
  endDate: string;
}

export interface AvailabilityResult {
  isAvailable: boolean;
  conflictingDates?: string[];
}

// Wishlist types
export interface WishlistItem {
  id: string;
  propertyId: string;
  property: Property;
}

// KYC types
export interface KycStatus {
  status: string;
  validatedAt: string | null;
  documents: KycDocument[];
}

export interface KycDocument {
  id: string;
  fileName: string;
  uploadedAt: string;
  processedAt: string | null;
}

// Notification types
export interface Notification {
  id: string;
  userId: string;
  type: string;
  subject: string;
  body: string;
  isRead: boolean;
  sentAt: string;
}

// Dashboard types
export interface OccupancyRate {
  propertyId: string;
  propertyName: string;
  totalDays: number;
  bookedDays: number;
  occupancyRate: number;
  period: string;
}

export interface RevenueData {
  propertyId: string;
  propertyName: string;
  totalRevenue: number;
  bookingCount: number;
  period: string;
}

export interface TrendDataPoint {
  period: string;
  bookings: number;
  revenue: number;
}

export interface BookingTrends {
  propertyId: string;
  period: string;
  dataPoints: TrendDataPoint[];
}

// Filter types
export interface PropertyFilters {
  city?: string;
  country?: string;
  startDate?: string;
  endDate?: string;
  minGuests?: number;
  page?: number;
  pageSize?: number;
}
