import { useCallback, useEffect, useState } from 'react';

// Runs `loader` (a memoised async function) whenever it or `refreshKey`
// changes and exposes { data, error, loading, reload }. While reloading, the
// previous `data` stays available so views can keep their frame. Stale
// responses are ignored.
export function useAsync(loader, refreshKey = 0) {
  const [reloadCount, setReloadCount] = useState(0);
  const [result, setResult] = useState({
    loader: null,
    reloadCount: -1,
    refreshKey: -1,
    data: undefined,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    loader().then(
      (data) => {
        if (!cancelled) {
          setResult({ loader, reloadCount, refreshKey, data, error: null });
        }
      },
      (error) => {
        if (!cancelled) {
          setResult({ loader, reloadCount, refreshKey, data: undefined, error });
        }
      }
    );

    return () => {
      cancelled = true;
    };
  }, [loader, reloadCount, refreshKey]);

  const reload = useCallback(() => setReloadCount((count) => count + 1), []);

  const loading =
    result.loader !== loader
    || result.reloadCount !== reloadCount
    || result.refreshKey !== refreshKey;

  return {
    data: result.data,
    error: loading ? null : result.error,
    loading,
    reload,
  };
}
