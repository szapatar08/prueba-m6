import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import type { ReactNode } from 'react';
import type { LoginResponse, UserProfile } from '../types';
import { authApi } from '../services/api';

interface AuthState {
  user: LoginResponse | null;
  profile: UserProfile | null;
  isAuthenticated: boolean;
  isOwner: boolean;
  isGuest: boolean;
  isLoading: boolean;
}

interface AuthContextType extends AuthState {
  login: (response: LoginResponse) => void;
  logout: () => void;
  refreshProfile: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<LoginResponse | null>(() => {
    const stored = localStorage.getItem('user');
    return stored ? JSON.parse(stored) : null;
  });
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const isAuthenticated = !!user && !!localStorage.getItem('token');
  const isOwner = user?.roles.includes('Owner') ?? false;
  const isGuest = user?.roles.includes('Guest') ?? false;

  const refreshProfile = useCallback(async () => {
    try {
      const { data } = await authApi.me();
      setProfile(data);
    } catch {
      // Token might be expired — logout silently
      setUser(null);
      setProfile(null);
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      refreshProfile().finally(() => setIsLoading(false));
    } else {
      setIsLoading(false);
    }
  }, [isAuthenticated, refreshProfile]);

  const login = (response: LoginResponse) => {
    localStorage.setItem('token', response.token);
    localStorage.setItem('user', JSON.stringify(response));
    setUser(response);
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    setUser(null);
    setProfile(null);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        profile,
        isAuthenticated,
        isOwner,
        isGuest,
        isLoading,
        login,
        logout,
        refreshProfile,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
