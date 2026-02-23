import { create } from 'zustand';
import apiClient from '@/services/apiClient';

interface AuthState {
  token: string | null;
  isAuthenticated: boolean;
  isLoggingOut: boolean;
  setToken: (token: string) => void;
  logout: () => Promise<void>;
  hydrate: () => void;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  token: null,
  isAuthenticated: false,
  isLoggingOut: false,
  setToken: (token: string) => {
    localStorage.setItem('auth_token', token);
    set({ token, isAuthenticated: true });
  },
  logout: async () => {
    if (get().isLoggingOut) return;
    set({ isLoggingOut: true });
    try {
      await apiClient.post('/auth/logout');
    } catch {
      // Proceed with local logout even if server call fails
    } finally {
      localStorage.removeItem('auth_token');
      set({ token: null, isAuthenticated: false, isLoggingOut: false });
    }
  },
  hydrate: () => {
    const token = localStorage.getItem('auth_token');
    if (token) {
      set({ token, isAuthenticated: true });
    }
  },
}));
