import { useMemo } from 'react';
import { useAuth } from '../contexts/AuthContext';
import * as catalogApi from '../services/catalogApi';

function withToken(fn, token) {
  return (...args) => {
    const lastArg = args.at(-1);
    const hasOptions =
      lastArg
      && typeof lastArg === 'object'
      && !Array.isArray(lastArg)
      && ('token' in lastArg || 'headers' in lastArg || 'signal' in lastArg);

    if (hasOptions) {
      return fn(...args.slice(0, -1), {
        ...lastArg,
        token: lastArg.token ?? token,
      });
    }

    return fn(...args, { token });
  };
}

export function useCatalogApi() {
  const { token } = useAuth();

  return useMemo(
    () =>
      Object.fromEntries(
        Object.entries(catalogApi).map(([name, fn]) => [
          name,
          withToken(fn, token),
        ]),
      ),
    [token],
  );
}
