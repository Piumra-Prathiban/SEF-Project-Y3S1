import { useMemo } from 'react';
import { useListQuery } from '../../../hooks/useListQuery';
import { resolveDateRange } from './analyticsUtils';

const DEFAULTS = { range: '30', from: '', to: '', page: 1 };

// Date range selection kept in the URL (?range=30 or ?range=custom&from=&to=).
export function useDateRange() {
  const [query, setQuery] = useListQuery(DEFAULTS);

  const apiRange = useMemo(
    () => resolveDateRange(query),
    [query]
  );

  return { selection: query, apiRange, setSelection: setQuery };
}
