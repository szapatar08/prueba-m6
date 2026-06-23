import { useState, useEffect, useCallback } from 'react';
import { propertiesApi, wishlistApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import PropertyCard from '../components/PropertyCard';
import type { Property, PropertyFilters } from '../types';

export default function Catalog() {
  const [properties, setProperties] = useState<Property[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [wishlistIds, setWishlistIds] = useState<Set<string>>(new Set());
  const { isAuthenticated, isGuest } = useAuth();

  const [filters, setFilters] = useState<PropertyFilters>({
    city: '',
    country: '',
    startDate: '',
    endDate: '',
    minGuests: undefined,
  });

  const fetchProperties = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const params: PropertyFilters = { page, pageSize: 12 };
      if (filters.city) params.city = filters.city;
      if (filters.country) params.country = filters.country;
      if (filters.startDate) params.startDate = filters.startDate;
      if (filters.endDate) params.endDate = filters.endDate;
      if (filters.minGuests) params.minGuests = filters.minGuests;

      const { data } = await propertiesApi.browse(params);
      setProperties(data.properties);
      setTotalCount(data.totalCount);
    } catch {
      setError('Failed to load properties');
    } finally {
      setLoading(false);
    }
  }, [page, filters]);

  const fetchWishlist = useCallback(async () => {
    if (!isAuthenticated || !isGuest) return;
    try {
      const { data } = await wishlistApi.get();
      setWishlistIds(new Set(data.map((item) => item.propertyId)));
    } catch {
      // Silently fail — wishlist is optional
    }
  }, [isAuthenticated, isGuest]);

  useEffect(() => {
    fetchProperties();
  }, [fetchProperties]);

  useEffect(() => {
    fetchWishlist();
  }, [fetchWishlist]);

  const handleToggleWishlist = async (propertyId: string) => {
    try {
      if (wishlistIds.has(propertyId)) {
        await wishlistApi.remove(propertyId);
        setWishlistIds((prev) => {
          const next = new Set(prev);
          next.delete(propertyId);
          return next;
        });
      } else {
        await wishlistApi.add(propertyId);
        setWishlistIds((prev) => new Set(prev).add(propertyId));
      }
    } catch {
      // Silently fail
    }
  };

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    fetchProperties();
  };

  const totalPages = Math.ceil(totalCount / 12);

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">Find your perfect stay</h1>

      {/* Search filters */}
      <form onSubmit={handleSearch} className="bg-white rounded-lg shadow-sm border border-gray-200 p-4 mb-8">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">City</label>
            <input
              type="text"
              value={filters.city || ''}
              onChange={(e) => setFilters({ ...filters, city: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Any city"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Country</label>
            <input
              type="text"
              value={filters.country || ''}
              onChange={(e) => setFilters({ ...filters, country: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Any country"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Check-in</label>
            <input
              type="date"
              value={filters.startDate || ''}
              onChange={(e) => setFilters({ ...filters, startDate: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Check-out</label>
            <input
              type="date"
              value={filters.endDate || ''}
              onChange={(e) => setFilters({ ...filters, endDate: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Guests</label>
            <input
              type="number"
              min="1"
              value={filters.minGuests || ''}
              onChange={(e) => setFilters({ ...filters, minGuests: e.target.value ? Number(e.target.value) : undefined })}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Min guests"
            />
          </div>
        </div>
        <div className="mt-4 flex justify-end">
          <button
            type="submit"
            className="px-6 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
          >
            Search
          </button>
        </div>
      </form>

      {/* Results */}
      {error && (
        <div className="rounded-md bg-red-50 p-4 mb-6">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {loading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
        </div>
      ) : properties.length === 0 ? (
        <div className="text-center py-12">
          <p className="text-gray-500 text-lg">No properties found. Try adjusting your filters.</p>
        </div>
      ) : (
        <>
          <p className="text-sm text-gray-500 mb-4">{totalCount} properties found</p>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {properties.map((property) => (
              <PropertyCard
                key={property.id}
                property={property}
                isInWishlist={wishlistIds.has(property.id)}
                onToggleWishlist={handleToggleWishlist}
                showWishlistButton={isAuthenticated && isGuest}
              />
            ))}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="mt-8 flex justify-center space-x-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
                className="px-3 py-2 border border-gray-300 rounded-md text-sm disabled:opacity-50 hover:bg-gray-50"
              >
                Previous
              </button>
              <span className="px-3 py-2 text-sm text-gray-700">
                Page {page} of {totalPages}
              </span>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
                className="px-3 py-2 border border-gray-300 rounded-md text-sm disabled:opacity-50 hover:bg-gray-50"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
