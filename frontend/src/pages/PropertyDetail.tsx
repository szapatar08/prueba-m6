import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { propertiesApi, wishlistApi, bookingsApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import type { PropertyDetail as PropertyDetailType } from '../types';
import { format, addDays } from 'date-fns';

export default function PropertyDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { isAuthenticated, isGuest, isOwner, user } = useAuth();
  const [property, setProperty] = useState<PropertyDetailType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [isInWishlist, setIsInWishlist] = useState(false);

  // Booking form state
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [bookingLoading, setBookingLoading] = useState(false);
  const [bookingError, setBookingError] = useState('');
  const [bookingSuccess, setBookingSuccess] = useState(false);

  const isOwnerOfProperty = isOwner && user && property?.ownerId === user.userId;

  const fetchProperty = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const { data } = await propertiesApi.getById(id);
      setProperty(data);
    } catch {
      setError('Property not found');
    } finally {
      setLoading(false);
    }
  }, [id]);

  const checkWishlist = useCallback(async () => {
    if (!id || !isAuthenticated || !isGuest) return;
    try {
      const { data } = await wishlistApi.check(id);
      setIsInWishlist(data.isInWishlist);
    } catch {
      // Silently fail
    }
  }, [id, isAuthenticated, isGuest]);

  useEffect(() => {
    fetchProperty();
  }, [fetchProperty]);

  useEffect(() => {
    checkWishlist();
  }, [checkWishlist]);

  const handleToggleWishlist = async () => {
    if (!id) return;
    try {
      if (isInWishlist) {
        await wishlistApi.remove(id);
      } else {
        await wishlistApi.add(id);
      }
      setIsInWishlist(!isInWishlist);
    } catch {
      // Silently fail
    }
  };

  const calculateTotalPrice = () => {
    if (!property || !startDate || !endDate) return 0;
    const start = new Date(startDate);
    const end = new Date(endDate);
    const nights = Math.ceil((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24));
    return nights > 0 ? nights * property.pricePerNight : 0;
  };

  const handleBook = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id || !property) return;

    setBookingError('');
    setBookingLoading(true);

    try {
      await bookingsApi.create({
        propertyId: id,
        startDate,
        endDate,
        totalPrice: calculateTotalPrice(),
      });
      setBookingSuccess(true);
      setTimeout(() => navigate('/bookings'), 2000);
    } catch (err: unknown) {
      const axiosError = err as { response?: { data?: { error?: string } } };
      setBookingError(axiosError.response?.data?.error || 'Booking failed. Please try again.');
    } finally {
      setBookingLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
      </div>
    );
  }

  if (error || !property) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-12 text-center">
        <p className="text-red-600 text-lg">{error || 'Property not found'}</p>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <button onClick={() => navigate(-1)} className="mb-4 text-sm text-blue-600 hover:text-blue-800">
        &larr; Back
      </button>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Property details */}
        <div className="lg:col-span-2">
          {/* Images */}
          <div className="rounded-lg overflow-hidden bg-gray-200 h-96">
            {property.images.length > 0 ? (
              <img
                src={property.images[0].url}
                alt={property.name}
                className="w-full h-full object-cover"
              />
            ) : (
              <div className="w-full h-full flex items-center justify-center">
                <svg className="w-16 h-16 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                </svg>
              </div>
            )}
          </div>

          {/* Image gallery */}
          {property.images.length > 1 && (
            <div className="mt-4 grid grid-cols-4 gap-2">
              {property.images.slice(1, 5).map((img) => (
                <div key={img.id} className="h-20 rounded overflow-hidden bg-gray-200">
                  <img src={img.url} alt="" className="w-full h-full object-cover" />
                </div>
              ))}
            </div>
          )}

          {/* Info */}
          <div className="mt-6">
            <div className="flex items-start justify-between">
              <div>
                <h1 className="text-3xl font-bold text-gray-900">{property.name}</h1>
                <p className="mt-1 text-gray-500">
                  {property.location}, {property.city}, {property.country}
                </p>
              </div>
              {isAuthenticated && isGuest && (
                <button
                  onClick={handleToggleWishlist}
                  className="p-2 rounded-full hover:bg-gray-100"
                >
                  <svg
                    className={`w-8 h-8 ${isInWishlist ? 'text-red-500 fill-current' : 'text-gray-400'}`}
                    fill={isInWishlist ? 'currentColor' : 'none'}
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z" />
                  </svg>
                </button>
              )}
            </div>

            <div className="mt-4 flex items-center space-x-4 text-gray-600">
              <span className="flex items-center">
                <span className="font-medium">{property.bedrooms}</span>&nbsp;bedroom{property.bedrooms !== 1 ? 's' : ''}
              </span>
              <span>&middot;</span>
              <span className="flex items-center">
                <span className="font-medium">{property.bathrooms}</span>&nbsp;bathroom{property.bathrooms !== 1 ? 's' : ''}
              </span>
              <span>&middot;</span>
              <span className="flex items-center">
                <span className="font-medium">{property.maxGuests}</span>&nbsp;guest{property.maxGuests !== 1 ? 's' : ''} max
              </span>
            </div>

            <div className="mt-6">
              <h2 className="text-lg font-semibold text-gray-900">About this property</h2>
              <p className="mt-2 text-gray-600 whitespace-pre-line">{property.description}</p>
            </div>

            <div className="mt-6">
              <h2 className="text-lg font-semibold text-gray-900">Address</h2>
              <p className="mt-2 text-gray-600">{property.address}</p>
            </div>
          </div>
        </div>

        {/* Booking sidebar */}
        <div className="lg:col-span-1">
          <div className="sticky top-8 bg-white rounded-lg shadow-md border border-gray-200 p-6">
            <div className="text-2xl font-bold text-gray-900">
              ${property.pricePerNight}
              <span className="text-base font-normal text-gray-500"> / night</span>
            </div>

            {isAuthenticated && isGuest ? (
              <form onSubmit={handleBook} className="mt-6 space-y-4">
                {bookingError && (
                  <div className="rounded-md bg-red-50 p-3">
                    <p className="text-sm text-red-700">{bookingError}</p>
                  </div>
                )}
                {bookingSuccess && (
                  <div className="rounded-md bg-green-50 p-3">
                    <p className="text-sm text-green-700">Booking created! Redirecting...</p>
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-gray-700">Check-in</label>
                  <input
                    type="date"
                    required
                    min={format(addDays(new Date(), 1), 'yyyy-MM-dd')}
                    value={startDate}
                    onChange={(e) => setStartDate(e.target.value)}
                    className="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700">Check-out</label>
                  <input
                    type="date"
                    required
                    min={startDate || format(addDays(new Date(), 2), 'yyyy-MM-dd')}
                    value={endDate}
                    onChange={(e) => setEndDate(e.target.value)}
                    className="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  />
                </div>

                {startDate && endDate && (
                  <div className="border-t border-gray-200 pt-4">
                    <div className="flex justify-between text-sm">
                      <span className="text-gray-600">Total</span>
                      <span className="font-bold text-gray-900">${calculateTotalPrice()}</span>
                    </div>
                  </div>
                )}

                <button
                  type="submit"
                  disabled={bookingLoading || bookingSuccess}
                  className="w-full py-3 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {bookingLoading ? 'Booking...' : 'Reserve'}
                </button>
              </form>
            ) : isOwnerOfProperty ? (
              <div className="mt-6 space-y-3">
                <button
                  onClick={() => navigate(`/properties/${id}/edit`)}
                  className="w-full py-2 px-4 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50"
                >
                  Edit Property
                </button>
                <button
                  onClick={() => navigate(`/properties/${id}/bookings`)}
                  className="w-full py-2 px-4 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50"
                >
                  View Bookings
                </button>
              </div>
            ) : !isAuthenticated ? (
              <div className="mt-6">
                <button
                  onClick={() => navigate('/login', { state: { from: { pathname: `/properties/${id}` } } })}
                  className="w-full py-3 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700"
                >
                  Sign in to book
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}
