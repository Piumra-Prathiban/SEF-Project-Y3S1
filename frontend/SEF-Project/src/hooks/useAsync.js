import { useCallback, useEffect, useState } from 'react';

// Runs `loader` (a memoised async function) whenever it changes and exposes
// { data, error, loading, reload }. Stale responses are ignored.
export function useAsync(loader) {
  const [reloadCount, setReloadCount] = useState(0);
  const [result, setResult] = useState({
    loader: null,
    reloadCount: -1,
    data: undefined,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    loader().then(
      (data) => {
        if (!cancelled) {
          setResult({ loader, reloadCount, data, error: null });
        }
      },
      (error) => {
        if (!cancelled) {
          setResult({ loader, reloadCount, data: undefined, error });
        }
      }
    );

    return () => {
      cancelled = true;
    };
  }, [loader, reloadCount]);

  const reload = useCallback(() => setReloadCount((count) => count + 1), []);

  const loading =
    result.loader !== loader || result.reloadCount !== reloadCount;

  return {
    data: result.data,
    error: loading ? null : result.error,
    loading,
    reload,
  };
}
