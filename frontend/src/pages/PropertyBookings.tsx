import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { bookingsApi } from '../services/api';
import type { Booking } from '../types';
import { format } from 'date-fns';

const statusColors: Record<string, string> = {
  Pending: 'bg-yellow-100 text-yellow-800',
  Confirmed: 'bg-green-100 text-green-800',
  Completed: 'bg-blue-100 text-blue-800',
  Cancelled: 'bg-red-100 text-red-800',
};

export default function PropertyBookings() {
  const { propertyId } = useParams<{ propertyId: string }>();
  const navigate = useNavigate();
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchBookings = useCallback(async () => {
    if (!propertyId) return;
    setLoading(true);
    try {
      const { data } = await bookingsApi.getPropertyBookings(propertyId);
      setBookings(data);
    } catch {
      setError('Failed to load bookings');
    } finally {
      setLoading(false);
    }
  }, [propertyId]);

  useEffect(() => {
    fetchBookings();
  }, [fetchBookings]);

  const handleConfirm = async (id: string) => {
    try {
      await bookingsApi.confirm(id);
      setBookings((prev) =>
        prev.map((b) => (b.id === id ? { ...b, status: 'Confirmed' as const } : b))
      );
    } catch {
      setError('Failed to confirm booking');
    }
  };

  const handleComplete = async (id: string) => {
    try {
      await bookingsApi.complete(id);
      setBookings((prev) =>
        prev.map((b) => (b.id === id ? { ...b, status: 'Completed' as const } : b))
      );
    } catch {
      setError('Failed to complete booking');
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
      <button onClick={() => navigate(-1)} className="mb-4 text-sm text-blue-600 hover:text-blue-800">
        &larr; Back
      </button>
      <h1 className="text-3xl font-bold text-gray-900 mb-8">Property Bookings</h1>

      {error && (
        <div className="rounded-md bg-red-50 p-4 mb-6">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {bookings.length === 0 ? (
        <div className="text-center py-12">
          <p className="text-gray-500 text-lg">No bookings for this property yet.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {bookings.map((booking) => (
            <div key={booking.id} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm text-gray-500">
                    Guest: <span className="font-medium text-gray-700">{booking.guestEmail || 'Unknown'}</span>
                  </p>
                  <p className="mt-1 text-sm text-gray-500">
                    {format(new Date(booking.startDate), 'MMM d, yyyy')} &mdash;{' '}
                    {format(new Date(booking.endDate), 'MMM d, yyyy')}
                  </p>
                  <p className="mt-1 text-xs text-gray-400">
                    Created: {format(new Date(booking.createdAt), 'MMM d, yyyy h:mm a')}
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
                      onClick={() => handleConfirm(booking.id)}
                      className="px-3 py-1 border border-green-300 rounded-md text-sm font-medium text-green-600 hover:bg-green-50"
                    >
                      Confirm
                    </button>
                  )}
                  {booking.status === 'Confirmed' && (
                    <button
                      onClick={() => handleComplete(booking.id)}
                      className="px-3 py-1 border border-blue-300 rounded-md text-sm font-medium text-blue-600 hover:bg-blue-50"
                    >
                      Complete
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
