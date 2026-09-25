import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
} from 'react';
import {
  login as loginRequest,
  getMe,
} from '../services/authService';

const AuthContext = createContext(null);
const AUTH_STORAGE_KEY = 'sef.auth';

function readStoredAuth() {
  try {
    const value = window.localStorage.getItem(AUTH_STORAGE_KEY);
    return value ? JSON.parse(value) : null;
  } catch {
    return null;
  }
}

function writeStoredAuth(authState) {
  if (!authState) {
    window.localStorage.removeItem(AUTH_STORAGE_KEY);
    return;
  }

  window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(authState));
}

export function AuthProvider({ children }) {
  const [authState, setAuthState] = useState(() => readStoredAuth());

  const user = authState?.user ?? null;
  const token = authState?.token ?? null;
  const expiresAt = authState?.expiresAt ?? null;

  const login = useCallback(async (email, password) => {
    const response = await loginRequest(email, password);

    const nextState = {
      token: response.token,
      expiresAt: response.expiresAt,
      user: response.user,
    };

    setAuthState(nextState);
    writeStoredAuth(nextState);

    return response;
  }, []);

  const fetchCurrentUser = useCallback(async () => {
    if (!token) {
      return null;
    }

    const response = await getMe(token);

    const nextState = {
      token,
      expiresAt,
      user: {
        id: Number(response.userId),
        email: response.email,
        role: response.role,
      },
    };

    setAuthState(nextState);
    writeStoredAuth(nextState);

    return response;
  }, [expiresAt, token]);

  const logout = useCallback(() => {
    setAuthState(null);
    writeStoredAuth(null);
  }, []);

  const value = useMemo(
    () => ({
      user,
      token,
      expiresAt,
      isAuthenticated: token !== null,
      isStaffOrAdmin:
        user?.role === 'Staff' || user?.role === 'Administrator',
      login,
      logout,
      fetchCurrentUser,
    }),
    [user, token, expiresAt, login, logout, fetchCurrentUser],
  );

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside an AuthProvider');
  }

  return context;
}
