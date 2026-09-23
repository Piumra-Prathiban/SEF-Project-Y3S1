import { useEffect, useState } from 'react';

export function navigateTo(path) {
  window.history.pushState({}, '', path);
  window.dispatchEvent(new PopStateEvent('popstate'));
}

export function useLocation() {
  const [location, setLocation] = useState(() => window.location.pathname);

  useEffect(() => {
    function handleLocationChange() {
      setLocation(window.location.pathname);
    }

    window.addEventListener('popstate', handleLocationChange);

    return () => {
      window.removeEventListener('popstate', handleLocationChange);
    };
  }, []);

  return location;
}
