import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { useAuth } from '../../contexts/AuthContext';
import { isStaff } from '../../utils/roles';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import './LoginPage.css';

export function LoginPage() {
  const { isAuthenticated, login, user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const returnTo = location.state?.from
    ? `${location.state.from.pathname}${location.state.from.search ?? ''}`
    : null;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  if (isAuthenticated) {
    return <Navigate to={returnTo ?? (isStaff(user) ? '/dashboard' : '/orders')} replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();

    setError(null);
    setIsSubmitting(true);

    try {
      const response = await login(email, password);
      const landing = isStaff(response?.user) ? '/dashboard' : '/orders';

      navigate(returnTo ?? landing, { replace: true });
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-story" aria-label="About Clothic">
        <Link className="auth-story__brand" to="/">CLOTHIC<span>®</span></Link>
        <div className="auth-story__content"><p className="eyebrow">THE ART OF EVERYDAY DRESSING</p><h2>Style that stays with you.</h2><p>Discover pieces you love, keep every order close, and make room for what comes next.</p></div>
        <p className="auth-story__foot">Thoughtful fashion. Effortless shopping.</p>
      </section>
      <section className="auth-content" aria-labelledby="auth-title">
        <div className="auth-content__top"><Link to="/">← Back to the store</Link><span>New here? <Link state={{ from: location.state?.from }} to="/register">Create an account</Link></span></div>
        <div className="auth-content__center">
          <p className="eyebrow">WELCOME BACK</p>
          <h1 id="auth-title">Sign in</h1>
          <p>Pick up where you left off.</p>
          <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            Email address
            <input
              autoComplete="email"
              placeholder="you@example.com"
              onChange={(event) => setEmail(event.target.value)}
              required
              type="email"
              value={email}
            />
          </label>

          <label>
            Password
            <span className="auth-password"><input
              autoComplete="current-password"
              onChange={(event) => setPassword(event.target.value)}
              required
              type={showPassword ? 'text' : 'password'}
              value={password}
            /><button aria-label={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword((visible) => !visible)} type="button">{showPassword ? 'Hide' : 'Show'}</button></span>
          </label>

          <button className="auth-submit" disabled={isSubmitting} type="submit">
            {isSubmitting ? 'Signing in...' : 'Sign in'} <span aria-hidden="true">→</span>
          </button>
        </form>

        <ApiErrorAlert message={error} />
        <p className="auth-content__switch">Don’t have an account? <Link state={{ from: location.state?.from }} to="/register">Join Clothic</Link></p>
        </div>
        <p className="auth-content__bottom">Clothic © {new Date().getFullYear()}</p>
      </section>
    </main>
  );
}

export default LoginPage;
