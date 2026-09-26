import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { useAuth } from '../../contexts/AuthContext';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import './LoginPage.css';

export default function RegisterPage() {
  const { isAuthenticated, register } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const returnTo = location.state?.from?.pathname
    ? `${location.state.from.pathname}${location.state.from.search || ''}`
    : '/orders';
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', password: '' });
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  if (isAuthenticated) return <Navigate to={returnTo} replace />;

  async function handleSubmit(event) {
    event.preventDefault();
    if (form.password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await register(form);
      navigate(returnTo, { replace: true });
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-story" aria-label="About Clothic">
        <Link className="auth-story__brand" to="/">CLOTHIC<span>®</span></Link>
        <div className="auth-story__content"><p className="eyebrow">WELCOME TO CLOTHIC</p><h2>A wardrobe that feels like you.</h2><p>Save your favourites, discover your next everyday piece, and follow each order from checkout to delivery.</p></div>
        <p className="auth-story__foot">Thoughtful fashion. Effortless shopping.</p>
      </section>
      <section className="auth-content" aria-labelledby="register-title">
        <div className="auth-content__top"><Link to="/">← Back to the store</Link><span>Already a member? <Link state={{ from: location.state?.from }} to="/login">Sign in</Link></span></div>
        <div className="auth-content__center">
          <p className="eyebrow">YOUR CLOTHIC ACCOUNT</p><h1 id="register-title">Create an account</h1><p>Make your shopping experience yours.</p>
          <form className="auth-form" onSubmit={handleSubmit}>
            <div className="auth-form__row"><label>First name<input autoComplete="given-name" maxLength="50" onChange={(event) => setForm({ ...form, firstName: event.target.value })} required value={form.firstName} /></label><label>Last name<input autoComplete="family-name" maxLength="50" onChange={(event) => setForm({ ...form, lastName: event.target.value })} required value={form.lastName} /></label></div>
            <label>Email address<input autoComplete="email" onChange={(event) => setForm({ ...form, email: event.target.value })} placeholder="you@example.com" required type="email" value={form.email} /></label>
            <label>Password<input autoComplete="new-password" minLength="8" onChange={(event) => setForm({ ...form, password: event.target.value })} required type="password" value={form.password} /><small>At least 8 characters.</small></label>
            <label>Confirm password<input autoComplete="new-password" minLength="8" onChange={(event) => setConfirmPassword(event.target.value)} required type="password" value={confirmPassword} /></label>
            <ApiErrorAlert message={error} />
            <button className="auth-submit" disabled={submitting} type="submit">{submitting ? 'Creating account...' : 'Create account'} <span aria-hidden="true">→</span></button>
          </form>
          <p className="auth-content__switch">Already have an account? <Link state={{ from: location.state?.from }} to="/login">Sign in</Link></p>
        </div>
        <p className="auth-content__bottom">Clothic © {new Date().getFullYear()}</p>
      </section>
    </main>
  );
}
