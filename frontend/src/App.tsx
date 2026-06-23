import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import Navbar from './components/Navbar';
import ProtectedRoute from './components/ProtectedRoute';
import Catalog from './pages/Catalog';
import Login from './pages/Login';
import Register from './pages/Register';
import PropertyDetail from './pages/PropertyDetail';
import CreateProperty from './pages/CreateProperty';
import MyBookings from './pages/MyBookings';
import PropertyBookings from './pages/PropertyBookings';
import Wishlist from './pages/Wishlist';
import Dashboard from './pages/Dashboard';
import KycUpload from './pages/KycUpload';
import Notifications from './pages/Notifications';
import Reports from './pages/Reports';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Navbar />
          <Routes>
            {/* Public routes */}
            <Route path="/" element={<Catalog />} />
            <Route path="/login" element={<Login />} />
            <Route path="/register" element={<Register />} />
            <Route path="/properties/:id" element={<PropertyDetail />} />

            {/* Guest routes */}
            <Route
              path="/bookings"
              element={
                <ProtectedRoute roles={['Guest']}>
                  <MyBookings />
                </ProtectedRoute>
              }
            />
            <Route
              path="/wishlist"
              element={
                <ProtectedRoute roles={['Guest']}>
                  <Wishlist />
                </ProtectedRoute>
              }
            />

            {/* Owner routes */}
            <Route
              path="/properties/new"
              element={
                <ProtectedRoute roles={['Owner']}>
                  <CreateProperty />
                </ProtectedRoute>
              }
            />
            <Route
              path="/properties/:propertyId/bookings"
              element={
                <ProtectedRoute roles={['Owner']}>
                  <PropertyBookings />
                </ProtectedRoute>
              }
            />
            <Route
              path="/dashboard"
              element={
                <ProtectedRoute roles={['Owner']}>
                  <Dashboard />
                </ProtectedRoute>
              }
            />
            <Route
              path="/reports"
              element={
                <ProtectedRoute roles={['Owner']}>
                  <Reports />
                </ProtectedRoute>
              }
            />

            {/* Authenticated routes */}
            <Route
              path="/kyc"
              element={
                <ProtectedRoute>
                  <KycUpload />
                </ProtectedRoute>
              }
            />
            <Route
              path="/notifications"
              element={
                <ProtectedRoute>
                  <Notifications />
                </ProtectedRoute>
              }
            />
          </Routes>
        </div>
      </AuthProvider>
    </BrowserRouter>
  );
}
