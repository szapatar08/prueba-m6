import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { bookingsApi } from '../services/api';
import type { Booking } from '../types';
import { format } from 'date-fns';

const statusColors: Record<string, string> = {
  Pending: 'bg-yellow-100 text-yellow-800',
  Confirmed: 'bg-green-100 text-green-800',
  Completed: 'bg-blue-100 text-blue-800',
  Cancelled: 'bg-red-100 text-red-800',
};

export default function MyBookings() {
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchBookings = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await bookingsApi.getMyBookings();
      setBookings(data);
    } catch {
      setError('Failed to load bookings');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchBookings();
  }, [fetchBookings]);

  const handleCancel = async (id: string) => {
    try {
      await bookingsApi.cancel(id);
      setBookings((prev) =>
        prev.map((b) => (b.id === id ? { ...b, status: 'Cancelled' as const } : b))
      );
    } catch {
      setError('Failed to cancel booking');
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
      <h1 className="text-3xl font-bold text-gray-900 mb-8">My Bookings</h1>

      {error && (
        <div className="rounded-md bg-red-50 p-4 mb-6">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {bookings.length === 0 ? (
        <div className="text-center py-12">
          <p className="text-gray-500 text-lg mb-4">You don't have any bookings yet.</p>
          <Link to="/" className="text-blue-600 hover:text-blue-800 font-medium">
            Browse properties &rarr;
          </Link>
        </div>
      ) : (
        <div className="space-y-4">
          {bookings.map((booking) => (
            <div key={booking.id} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900">
                    {booking.propertyName || 'Property'}
                  </h3>
                  <p className="mt-1 text-sm text-gray-500">
                    {format(new Date(booking.startDate), 'MMM d, yyyy')} &mdash;{' '}
                    {format(new Date(booking.endDate), 'MMM d, yyyy')}
                  </p>
                </div>

                <div className="mt-4 sm:mt-0 flex items-center space-x-4">
                  <span className={`px-3 py-1 rounded-full text-xs font-medium ${statusColors[booking.status] || 'bg-gray-100 text-gray-800'}`}>
                    {booking.status}
                  </span>
                  <span className="text-lg font-bold text-gray-900">
                    ${booking.totalPrice}
                  </span>
                  {booking.status === 'Pending' && (
                    <button
                      onClick={() => handleCancel(booking.id)}
                      className="px-3 py-1 border border-red-300 rounded-md text-sm font-medium text-red-600 hover:bg-red-50"
                    >
                      Cancel
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
