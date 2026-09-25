import { useState } from 'react';

const emptyAddress = {
  label: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  province: '',
  postalCode: '',
  country: 'Sri Lanka',
  isDefault: false,
};

export default function AddressForm({ initialValue, onSave, onCancel, submitting }) {
  const [address, setAddress] = useState(() => ({ ...emptyAddress, ...initialValue }));
  const [error, setError] = useState('');

  function setField(name, value) {
    setAddress((current) => ({ ...current, [name]: value }));
  }

  async function submit(event) {
    event.preventDefault();
    const required = ['label', 'addressLine1', 'city', 'postalCode', 'country'];
    if (required.some((field) => !address[field].trim())) {
      setError('Please complete every required address field.');
      return;
    }
    if (!/^[A-Za-z0-9][A-Za-z0-9 -]{1,19}$/.test(address.postalCode.trim())) {
      setError('Enter a valid postal code.');
      return;
    }
    setError('');
    await onSave(address);
  }

  return (
    <form className="address-form" onSubmit={submit}>
      <label>Label<input value={address.label} maxLength="50" placeholder="Home" onChange={(event) => setField('label', event.target.value)} required /></label>
      <label className="field-wide">Address line 1<input value={address.addressLine1} maxLength="200" onChange={(event) => setField('addressLine1', event.target.value)} required /></label>
      <label className="field-wide">Address line 2 <span>(optional)</span><input value={address.addressLine2} maxLength="200" onChange={(event) => setField('addressLine2', event.target.value)} /></label>
      <label>City<input value={address.city} maxLength="100" onChange={(event) => setField('city', event.target.value)} required /></label>
      <label>Province <span>(optional)</span><input value={address.province} maxLength="100" onChange={(event) => setField('province', event.target.value)} /></label>
      <label>Postal code<input value={address.postalCode} minLength="2" maxLength="20" onChange={(event) => setField('postalCode', event.target.value)} required /></label>
      <label>Country<input value={address.country} maxLength="100" onChange={(event) => setField('country', event.target.value)} required /></label>
      <label className="checkbox-field field-wide"><input type="checkbox" checked={address.isDefault} onChange={(event) => setField('isDefault', event.target.checked)} />Use as my default address</label>
      {error && <p className="form-error field-wide" role="alert">{error}</p>}
      <div className="form-actions field-wide"><button type="submit" className="button button--primary" disabled={submitting}>{submitting ? 'Saving…' : 'Save address'}</button><button type="button" className="button button--ghost" onClick={onCancel}>Cancel</button></div>
    </form>
  );
}
