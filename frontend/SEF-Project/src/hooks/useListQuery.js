import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

// Keeps list filters, sorting and paging in the URL so they survive reloads
// and can be shared. Changing any filter resets to page 1.
export function useListQuery(defaults) {
  const [searchParams, setSearchParams] = useSearchParams();
  const search = searchParams.toString();

  const query = useMemo(() => {
    const params = new URLSearchParams(search);
    const values = { ...defaults };

    Object.keys(defaults).forEach((key) => {
      if (params.has(key)) {
        values[key] = params.get(key);
      }
    });

    values.page = Number(values.page) || 1;
    return values;
    // `defaults` is a constant object supplied by each page.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const setQuery = useCallback(
    (changes) => {
      setSearchParams((current) => {
        const next = new URLSearchParams(current);

        Object.entries({ page: 1, ...changes }).forEach(([key, value]) => {
          if (value === '' || value === null || value === undefined) {
            next.delete(key);
          } else {
            next.set(key, String(value));
          }
        });

        if (next.get('page') === '1') {
          next.delete('page');
        }

        return next;
      });
    },
    [setSearchParams]
  );

  return [query, setQuery];
}
