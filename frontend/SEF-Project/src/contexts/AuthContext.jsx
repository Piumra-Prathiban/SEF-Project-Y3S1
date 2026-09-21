import { createContext, useContext, useState } from 'react';
import { login as loginRequest } from '../services/authService';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(null);
  const [expiresAt, setExpiresAt] = useState(null);

  async function login(email, password) {
    const response = await loginRequest(email, password);

    setToken(response.token);
    setExpiresAt(response.expiresAt);
    setUser(response.user);

    return response;
  }

  function logout() {
    setToken(null);
    setExpiresAt(null);
    setUser(null);
  }

  const value = {
    user,
    token,
    expiresAt,
    isAuthenticated: token !== null,
    login,
    logout,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside an AuthProvider');
  }

  return context;
}