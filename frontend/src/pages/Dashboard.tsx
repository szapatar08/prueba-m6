import { useState, useEffect, useCallback } from 'react';
import { dashboardApi, propertiesApi } from '../services/api';
import type { OccupancyRate, RevenueData, BookingTrends, Property } from '../types';
import { format, subMonths } from 'date-fns';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  BarChart,
  Bar,
} from 'recharts';

export default function Dashboard() {
  const [properties, setProperties] = useState<Property[]>([]);
  const [selectedProperty, setSelectedProperty] = useState('');
  const [startDate, setStartDate] = useState(format(subMonths(new Date(), 1), 'yyyy-MM-dd'));
  const [endDate, setEndDate] = useState(format(new Date(), 'yyyy-MM-dd'));
  const [period, setPeriod] = useState<'monthly' | 'weekly'>('monthly');
  const [occupancy, setOccupancy] = useState<OccupancyRate | null>(null);
  const [revenue, setRevenue] = useState<RevenueData | null>(null);
  const [trends, setTrends] = useState<BookingTrends | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const fetchProperties = useCallback(async () => {
    try {
      const { data } = await propertiesApi.browse({ pageSize: 100 });
      setProperties(data.properties);
      if (data.properties.length > 0) {
        setSelectedProperty(data.properties[0].id);
      }
    } catch {
      // Silently fail
    }
  }, []);

  useEffect(() => {
    fetchProperties();
  }, [fetchProperties]);

  const fetchDashboardData = useCallback(async () => {
    if (!selectedProperty) return;
    setLoading(true);
    setError('');

    try {
      const [occRes, revRes, trendsRes] = await Promise.all([
        dashboardApi.getOccupancy(selectedProperty, startDate, endDate),
        dashboardApi.getRevenue(selectedProperty, startDate, endDate),
        dashboardApi.getTrends(selectedProperty, period),
      ]);
      setOccupancy(occRes.data);
      setRevenue(revRes.data);
      setTrends(trendsRes.data);
    } catch {
      setError('Failed to load dashboard data');
    } finally {
      setLoading(false);
    }
  }, [selectedProperty, startDate, endDate, period]);

  useEffect(() => {
    fetchDashboardData();
  }, [fetchDashboardData]);

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">Owner Dashboard</h1>

      {/* Controls */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4 mb-8">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Property</label>
            <select
              value={selectedProperty}
              onChange={(e) => setSelectedProperty(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            >
              {properties.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">From</label>
            <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">To</label>
            <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Trend Period</label>
            <select
              value={period}
              onChange={(e) => setPeriod(e.target.value as 'monthly' | 'weekly')}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="monthly">Monthly</option>
              <option value="weekly">Weekly</option>
            </select>
          </div>
          <div className="flex items-end">
            <button
              onClick={fetchDashboardData}
              className="w-full px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700"
            >
              Refresh
            </button>
          </div>
        </div>
      </div>

      {error && (
        <div className="rounded-md bg-red-50 p-4 mb-6">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {loading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
        </div>
      ) : (
        <>
          {/* KPI Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <p className="text-sm font-medium text-gray-500">Occupancy Rate</p>
              <p className="mt-2 text-3xl font-bold text-gray-900">
                {occupancy ? `${(occupancy.occupancyRate * 100).toFixed(1)}%` : '--'}
              </p>
              <p className="mt-1 text-sm text-gray-500">
                {occupancy ? `${occupancy.bookedDays} / ${occupancy.totalDays} days` : ''}
              </p>
            </div>
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <p className="text-sm font-medium text-gray-500">Total Revenue</p>
              <p className="mt-2 text-3xl font-bold text-gray-900">
                {revenue ? `$${revenue.totalRevenue.toLocaleString()}` : '--'}
              </p>
              <p className="mt-1 text-sm text-gray-500">
                {revenue ? `${revenue.bookingCount} bookings` : ''}
              </p>
            </div>
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <p className="text-sm font-medium text-gray-500">Avg per Booking</p>
              <p className="mt-2 text-3xl font-bold text-gray-900">
                {revenue && revenue.bookingCount > 0
                  ? `$${(revenue.totalRevenue / revenue.bookingCount).toFixed(0)}`
                  : '--'}
              </p>
            </div>
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <p className="text-sm font-medium text-gray-500">Period</p>
              <p className="mt-2 text-lg font-bold text-gray-900">
                {occupancy?.period || '--'}
              </p>
            </div>
          </div>

          {/* Charts */}
          {trends && trends.dataPoints.length > 0 && (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <h2 className="text-lg font-semibold text-gray-900 mb-4">Revenue Trend</h2>
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={trends.dataPoints}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="period" />
                    <YAxis />
                    <Tooltip />
                    <Line type="monotone" dataKey="revenue" stroke="#2563eb" strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <h2 className="text-lg font-semibold text-gray-900 mb-4">Bookings Trend</h2>
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={trends.dataPoints}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="period" />
                    <YAxis />
                    <Tooltip />
                    <Bar dataKey="bookings" fill="#2563eb" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
