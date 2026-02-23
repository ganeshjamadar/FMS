import { create } from 'zustand';
import axios from 'axios';

const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000';

interface AuthState {
  token: string | null;
  isAuthenticated: boolean;
  isLoggingOut: boolean;
  setToken: (token: string | null) => void;
  logout: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  token: null,
  isAuthenticated: false,
  isLoggingOut: false,
  setToken: (token) => set({ token, isAuthenticated: !!token }),
  logout: async () => {
    if (get().isLoggingOut) return;
    const currentToken = get().token;
    set({ isLoggingOut: true });
    try {
      if (currentToken) {
        await axios.post(
          `${API_BASE_URL}/auth/logout`,
          null,
          { headers: { Authorization: `Bearer ${currentToken}` }, timeout: 5000 },
        );
      }
    } catch {
      // Proceed with local logout even if server call fails
    } finally {
      set({ token: null, isAuthenticated: false, isLoggingOut: false });
    }
  },
}));
