import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { useAuth } from '../../contexts/AuthContext';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import { apiRequest } from '../../services/api';
import { formatCurrency } from '../../utils/format';

export default function StylistPage() {
  const { token } = useAuth();
  const guardSessionExpiry = useSessionGuard();
  const [form, setForm] = useState({ occasion: '', budget: '', preferredSize: '', preferredColours: '', stylePreferences: '' });
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const response = await apiRequest('/recommendations', {
        method: 'POST', token,
        body: JSON.stringify({
          occasion: form.occasion.trim(),
          budget: form.budget ? Number(form.budget) : null,
          preferredSize: form.preferredSize.trim() || null,
          preferredColours: form.preferredColours.split(',').map((colour) => colour.trim()).filter(Boolean),
          stylePreferences: form.stylePreferences.trim() || null,
        }),
      });
      setResult(response);
    } catch (err) {
      if (!guardSessionExpiry(err)) setError(err?.message || 'Your recommendations could not be created.');
    } finally {
      setLoading(false);
    }
  }

  const recommendations = result?.recommendations ?? [];

  return (
    <div className="stylist-page">
      <div className="stylist-hero"><p className="eyebrow">YOUR PERSONAL STYLIST</p><h1>Find pieces for your moment.</h1><p>Tell us what you have in mind. We’ll look at the real collection and suggest available pieces within your preferences.</p></div>
      <div className="stylist-layout">
        <form className="stylist-form" onSubmit={handleSubmit}>
          <div><p className="eyebrow">TELL US A LITTLE</p><h2>What are you dressing for?</h2></div>
          <label>Occasion <input maxLength="100" minLength="2" onChange={(event) => update('occasion', event.target.value)} placeholder="A weekend away, a work event..." required value={form.occasion} /></label>
          <div className="stylist-form__row"><label>Budget (LKR) <input min="1" onChange={(event) => update('budget', event.target.value)} placeholder="Optional" type="number" value={form.budget} /></label><label>Preferred size <input maxLength="50" onChange={(event) => update('preferredSize', event.target.value)} placeholder="e.g. M" value={form.preferredSize} /></label></div>
          <label>Colours you like <input onChange={(event) => update('preferredColours', event.target.value)} placeholder="Black, cream, navy" value={form.preferredColours} /><small>Separate colours with commas.</small></label>
          <label>Style notes <textarea maxLength="500" onChange={(event) => update('stylePreferences', event.target.value)} placeholder="Minimal, relaxed, tailored..." rows="4" value={form.stylePreferences} /></label>
          <button className="button" disabled={loading} type="submit">{loading ? 'Finding your pieces...' : 'Find my pieces'} <span aria-hidden="true">↗</span></button>
          <ApiErrorAlert message={error} />
        </form>

        <section className="stylist-results" aria-live="polite">
          {!result && !loading && <div className="stylist-intro"><span aria-hidden="true">✦</span><p className="eyebrow">CURATED FOR YOU</p><h2>Your edit starts here.</h2><p>Share an occasion and we’ll bring the most relevant pieces together.</p></div>}
          {loading && <div className="stylist-intro"><span className="spinner" /><h2>Finding your edit...</h2><p>Checking the collection, sizes and availability.</p></div>}
          {result && <>
            <div className="stylist-results__heading"><p className="eyebrow">YOUR EDIT</p><h2>{recommendations.length ? 'Pieces picked for you.' : 'No matching pieces yet.'}</h2><p>{recommendations.length ? `${recommendations.length} available ${recommendations.length === 1 ? 'piece' : 'pieces'} for ${result.criteria?.occasion || form.occasion}.` : (result.execution?.errorSummary || 'Try a different occasion, budget or set of preferences.')}</p></div>
            {result.relaxedCriteria?.length > 0 && <p className="stylist-results__note">We widened the search to find these pieces: {result.relaxedCriteria.join(', ')}.</p>}
            {result.unappliedPreferences?.length > 0 && <p className="stylist-results__note">Some preferences could not be applied: {result.unappliedPreferences.join(', ')}.</p>}
            <div className="stylist-card-list">{recommendations.map((item) => <article className="stylist-card" key={item.variantId}><div className="stylist-card__mark" aria-hidden="true">C</div><div><p className="eyebrow">{item.variantName}</p><h3>{item.productName}</h3><p>{item.reason}</p><small>{[item.size, item.colour].filter(Boolean).join(' · ')}</small></div><div className="stylist-card__side"><strong>{formatCurrency(item.price, 'LKR')}</strong><Link to={`/shop/${item.productId}`}>View piece ↗</Link></div></article>)}</div>
            {!recommendations.length && <Link className="button button-secondary" to="/">Browse the collection</Link>}
          </>}
        </section>
      </div>
    </div>
  );
}
