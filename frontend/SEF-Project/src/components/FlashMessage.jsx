import { useLocation, useNavigate } from 'react-router-dom';
import { Alert } from './StatusViews';

// Shows a one-off success message passed via navigate(to, { state: { flash } }).
export default function FlashMessage() {
  const location = useLocation();
  const navigate = useNavigate();
  const flash = location.state?.flash;

  if (!flash) {
    return null;
  }

  return (
    <Alert
      tone="success"
      onDismiss={() =>
        navigate(`${location.pathname}${location.search}`, { replace: true, state: null })
      }
    >
      {flash}
    </Alert>
  );
}
