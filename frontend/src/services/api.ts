import axios from 'axios';
import type {
  LoginRequest,
  RegisterRequest,
  LoginResponse,
  RegisterResponse,
  UserProfile,
  BrowseCatalogResult,
  PropertyDetail,
  CreatePropertyRequest,
  Booking,
  CreateBookingRequest,
  AvailabilityResult,
  WishlistItem,
  KycStatus,
  Notification,
  OccupancyRate,
  RevenueData,
  BookingTrends,
  PropertyFilters,
} from '../types';

const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor — attach JWT token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Response interceptor — handle 401 globally
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

// --- Auth ---
export const authApi = {
  register: (data: RegisterRequest) =>
    api.post<RegisterResponse>('/auth/register', data),

  login: (data: LoginRequest) =>
    api.post<LoginResponse>('/auth/login', data),

  me: () => api.get<UserProfile>('/auth/me'),
};

// --- Properties ---
export const propertiesApi = {
  browse: (filters?: PropertyFilters) =>
    api.get<BrowseCatalogResult>('/properties', { params: filters }),

  getById: (id: string) =>
    api.get<PropertyDetail>(`/properties/${id}`),

  create: (data: CreatePropertyRequest) =>
    api.post<PropertyDetail>('/properties', data),

  update: (id: string, data: Partial<CreatePropertyRequest>) =>
    api.put<PropertyDetail>(`/properties/${id}`, data),

  delete: (id: string) =>
    api.delete(`/properties/${id}`),

  setAvailability: (id: string, data: { date: string; isAvailable: boolean }) =>
    api.post(`/properties/${id}/availability`, data),

  setAvailabilityRange: (id: string, data: { startDate: string; endDate: string; isAvailable: boolean }) =>
    api.post(`/properties/${id}/availability/range`, data),

  uploadImages: (id: string, images: { url: string; isPrimary: boolean }[]) =>
    api.post(`/properties/${id}/images`, images),
};

// --- Bookings ---
export const bookingsApi = {
  create: (data: CreateBookingRequest) =>
    api.post<Booking>('/bookings', data),

  getMyBookings: () =>
    api.get<Booking[]>('/bookings'),

  getPropertyBookings: (propertyId: string) =>
    api.get<Booking[]>(`/bookings/property/${propertyId}`),

  confirm: (id: string) =>
    api.put<Booking>(`/bookings/${id}/confirm`),

  cancel: (id: string) =>
    api.put<Booking>(`/bookings/${id}/cancel`),

  complete: (id: string) =>
    api.put<Booking>(`/bookings/${id}/complete`),

  checkAvailability: (propertyId: string, startDate: string, endDate: string) =>
    api.get<AvailabilityResult>('/bookings/availability', {
      params: { propertyId, startDate, endDate },
    }),
};

// --- Wishlist ---
export const wishlistApi = {
  get: () =>
    api.get<WishlistItem[]>('/wishlist'),

  add: (propertyId: string) =>
    api.post(`/wishlist/${propertyId}`),

  remove: (propertyId: string) =>
    api.delete(`/wishlist/${propertyId}`),

  check: (propertyId: string) =>
    api.get<{ isInWishlist: boolean }>(`/wishlist/${propertyId}/check`),
};

// --- KYC ---
export const kycApi = {
  upload: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post('/kyc/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  getStatus: () =>
    api.get<KycStatus>('/kyc/status'),
};

// --- Notifications ---
export const notificationsApi = {
  getAll: () =>
    api.get<Notification[]>('/notifications'),

  markAsRead: (id: string) =>
    api.put(`/notifications/${id}/read`),

  markAllAsRead: () =>
    api.put('/notifications/read-all'),
};

// --- Dashboard ---
export const dashboardApi = {
  getOccupancy: (propertyId: string, startDate: string, endDate: string) =>
    api.get<OccupancyRate>('/dashboard/occupancy', {
      params: { propertyId, startDate, endDate },
    }),

  getRevenue: (propertyId: string, startDate: string, endDate: string) =>
    api.get<RevenueData>('/dashboard/revenue', {
      params: { propertyId, startDate, endDate },
    }),

  getTrends: (propertyId: string, period: 'monthly' | 'weekly') =>
    api.get<BookingTrends>('/dashboard/trends', {
      params: { propertyId, period },
    }),
};

// --- Reports ---
export const reportsApi = {
  getPropertyReport: (propertyId: string, startDate: string, endDate: string) =>
    api.get(`/reports/property/${propertyId}`, {
      params: { startDate, endDate },
      responseType: 'blob',
    }),

  getPortfolioReport: (startDate: string, endDate: string) =>
    api.get('/reports/portfolio', {
      params: { startDate, endDate },
      responseType: 'blob',
    }),
};

export default api;
