import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { wishlistApi } from '../services/api';
import PropertyCard from '../components/PropertyCard';
import type { WishlistItem } from '../types';

export default function Wishlist() {
  const [items, setItems] = useState<WishlistItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchWishlist = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await wishlistApi.get();
      setItems(data);
    } catch {
      setError('Failed to load wishlist');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchWishlist();
  }, [fetchWishlist]);

  const handleRemove = async (propertyId: string) => {
    try {
      await wishlistApi.remove(propertyId);
      setItems((prev) => prev.filter((item) => item.propertyId !== propertyId));
    } catch {
      setError('Failed to remove from wishlist');
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">My Wishlist</h1>

      {error && (
        <div className="rounded-md bg-red-50 p-4 mb-6">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {items.length === 0 ? (
        <div className="text-center py-12">
          <p className="text-gray-500 text-lg mb-4">Your wishlist is empty.</p>
          <Link to="/" className="text-blue-600 hover:text-blue-800 font-medium">
            Browse properties &rarr;
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {items.map((item) => (
            <PropertyCard
              key={item.id}
              property={item.property}
              isInWishlist={true}
              onToggleWishlist={handleRemove}
              showWishlistButton={true}
            />
          ))}
        </div>
      )}
    </div>
  );
}
