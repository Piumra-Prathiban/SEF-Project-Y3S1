import { useCallback } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { apiRequest } from '../services/api';

export function useApi() {
  const { token } = useAuth();

  return useCallback(
    (endpoint, options = {}) =>
      apiRequest(endpoint, {
        ...options,
        token,
      }),
    [token],
  );
}
