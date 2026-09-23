import { useState } from 'react';
import {
  login as loginRequest,
  getMe,
} from '../services/authService';
import AuthContext from './AuthContextStore';

const SESSION_KEY = 'sef-customer-session';

function readStoredSession() {
  try {
    const session = JSON.parse(localStorage.getItem(SESSION_KEY));

    if (!session?.token || !session?.user) {
      return null;
    }

    if (session.expiresAt && new Date(session.expiresAt) <= new Date()) {
      localStorage.removeItem(SESSION_KEY);
      return null;
    }

    return session;
  } catch {
    localStorage.removeItem(SESSION_KEY);
    return null;
  }
}

export function AuthProvider({ children }) {
  const [initialSession] = useState(readStoredSession);
  const [user, setUser] = useState(initialSession?.user ?? null);
  const [token, setToken] = useState(initialSession?.token ?? null);
  const [expiresAt, setExpiresAt] = useState(initialSession?.expiresAt ?? null);

  async function login(email, password) {
    const response = await loginRequest(email, password);

    setToken(response.token);
    setExpiresAt(response.expiresAt);
    setUser(response.user);
    localStorage.setItem(SESSION_KEY, JSON.stringify(response));

    return response;
  }

  async function fetchCurrentUser() {
    if (!token) {
      return null;
    }

    const response = await getMe(token);

    setUser({
      id: Number(response.userId),
      email: response.email,
      role: response.role,
    });

    const nextSession = {
      token,
      expiresAt,
      user: {
        id: Number(response.userId),
        email: response.email,
        role: response.role,
      },
    };
    localStorage.setItem(SESSION_KEY, JSON.stringify(nextSession));

    return response;
  }

  function logout() {
    setToken(null);
    setExpiresAt(null);
    setUser(null);
    localStorage.removeItem(SESSION_KEY);
  }

  const value = {
    user,
    token,
    expiresAt,
    isAuthenticated: token !== null,
    login,
    logout,
    fetchCurrentUser,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}
