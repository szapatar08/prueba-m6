import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useState } from 'react';

export default function Navbar() {
  const { isAuthenticated, isOwner, isGuest, user, logout } = useAuth();
  const navigate = useNavigate();
  const [mobileOpen, setMobileOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <nav className="bg-white shadow-sm border-b border-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between h-16">
          <div className="flex items-center">
            <Link to="/" className="text-xl font-bold text-blue-600">
              Prueba
            </Link>
            <div className="hidden sm:ml-6 sm:flex sm:space-x-4">
              <Link
                to="/"
                className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
              >
                Properties
              </Link>
              {isAuthenticated && isGuest && (
                <>
                  <Link
                    to="/bookings"
                    className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
                  >
                    My Bookings
                  </Link>
                  <Link
                    to="/wishlist"
                    className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
                  >
                    Wishlist
                  </Link>
                </>
              )}
              {isAuthenticated && isOwner && (
                <>
                  <Link
                    to="/dashboard"
                    className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
                  >
                    Dashboard
                  </Link>
                  <Link
                    to="/reports"
                    className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
                  >
                    Reports
                  </Link>
                </>
              )}
              {isAuthenticated && (
                <>
                  <Link
                    to="/notifications"
                    className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
                  >
                    Notifications
                  </Link>
                  <Link
                    to="/kyc"
                    className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
                  >
                    KYC
                  </Link>
                </>
              )}
            </div>
          </div>

          <div className="hidden sm:flex sm:items-center sm:space-x-3">
            {isAuthenticated ? (
              <>
                <span className="text-sm text-gray-600">
                  {user?.firstName} {user?.lastName}
                </span>
                <span className="text-xs bg-blue-100 text-blue-800 px-2 py-0.5 rounded-full">
                  {user?.roles[0]}
                </span>
                <button
                  onClick={handleLogout}
                  className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-red-600 hover:bg-gray-50"
                >
                  Logout
                </button>
              </>
            ) : (
              <>
                <Link
                  to="/login"
                  className="px-3 py-2 rounded-md text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-gray-50"
                >
                  Login
                </Link>
                <Link
                  to="/register"
                  className="px-4 py-2 rounded-md text-sm font-medium text-white bg-blue-600 hover:bg-blue-700"
                >
                  Register
                </Link>
              </>
            )}
          </div>

          {/* Mobile menu button */}
          <div className="flex items-center sm:hidden">
            <button
              onClick={() => setMobileOpen(!mobileOpen)}
              className="p-2 rounded-md text-gray-400 hover:text-gray-500 hover:bg-gray-100"
            >
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                {mobileOpen ? (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                ) : (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                )}
              </svg>
            </button>
          </div>
        </div>
      </div>

      {/* Mobile menu */}
      {mobileOpen && (
        <div className="sm:hidden border-t border-gray-200">
          <div className="px-2 pt-2 pb-3 space-y-1">
            <Link to="/" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">Properties</Link>
            {isAuthenticated && isGuest && (
              <>
                <Link to="/bookings" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">My Bookings</Link>
                <Link to="/wishlist" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">Wishlist</Link>
              </>
            )}
            {isAuthenticated && isOwner && (
              <>
                <Link to="/dashboard" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">Dashboard</Link>
                <Link to="/reports" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">Reports</Link>
              </>
            )}
            {isAuthenticated && (
              <>
                <Link to="/notifications" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">Notifications</Link>
                <Link to="/kyc" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">KYC</Link>
              </>
            )}
            {isAuthenticated ? (
              <button onClick={() => { handleLogout(); setMobileOpen(false); }} className="w-full text-left px-3 py-2 rounded-md text-base font-medium text-red-600 hover:bg-gray-50">Logout</button>
            ) : (
              <>
                <Link to="/login" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:bg-gray-50">Login</Link>
                <Link to="/register" onClick={() => setMobileOpen(false)} className="block px-3 py-2 rounded-md text-base font-medium text-blue-600 hover:bg-gray-50">Register</Link>
              </>
            )}
          </div>
        </div>
      )}
    </nav>
  );
}
