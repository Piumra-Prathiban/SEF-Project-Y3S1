import { useCallback, useEffect, useState } from 'react';
import AddressForm from '../components/AddressForm';
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState';
import { useAuth } from '../hooks/useAuth';
import { getApiErrorMessage } from '../services/api';
import { createAddress, deleteAddress, getAddresses, getProfile, updateAddress, updateProfile } from '../services/shoppingService';

function toAddressRequest(address) {
  return {
    label: address.label,
    addressLine1: address.addressLine1,
    addressLine2: address.addressLine2 || '',
    city: address.city,
    province: address.province || '',
    postalCode: address.postalCode,
    country: address.country,
    isDefault: address.isDefault,
  };
}

export default function ProfilePage() {
  const { token } = useAuth();
  const [profile, setProfile] = useState(null);
  const [profileForm, setProfileForm] = useState({ email: '', firstName: '', lastName: '' });
  const [addresses, setAddresses] = useState([]);
  const [editingAddress, setEditingAddress] = useState(null);
  const [showAddressForm, setShowAddressForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const load = useCallback(async () => {
    await Promise.resolve();
    setLoading(true);
    setError('');
    try {
      const [profileResponse, addressResponse] = await Promise.all([getProfile(token), getAddresses(token)]);
      setProfile(profileResponse);
      setProfileForm({ email: profileResponse.email, firstName: profileResponse.firstName, lastName: profileResponse.lastName });
      setAddresses(addressResponse);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    const timer = window.setTimeout(load, 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  async function saveProfile(event) {
    event.preventDefault();
    if (!profileForm.firstName.trim() || !profileForm.lastName.trim() || !profileForm.email.trim()) {
      setError('Name and email fields are required.');
      return;
    }
    setSubmitting(true);
    setError('');
    try {
      const response = await updateProfile(token, profileForm);
      setProfile(response);
      setProfileForm({ email: response.email, firstName: response.firstName, lastName: response.lastName });
      setNotice('Profile updated.');
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  }

  async function saveAddress(address) {
    setSubmitting(true);
    setError('');
    try {
      const request = toAddressRequest(address);
      if (editingAddress) await updateAddress(token, editingAddress.id, request);
      else await createAddress(token, request);
      setEditingAddress(null);
      setShowAddressForm(false);
      setAddresses(await getAddresses(token));
      setNotice(editingAddress ? 'Address updated.' : 'Address added.');
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  }

  async function removeAddress(addressId) {
    setSubmitting(true);
    setError('');
    try {
      await deleteAddress(token, addressId);
      setAddresses(await getAddresses(token));
      setNotice('Address removed.');
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  }

  async function makeDefault(address) {
    setSubmitting(true);
    setError('');
    try {
      await updateAddress(token, address.id, toAddressRequest({ ...address, isDefault: true }));
      setAddresses(await getAddresses(token));
      setNotice('Default address updated.');
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) return <LoadingState message="Loading your profile…" />;
  if (error && !profile) return <ErrorState message={error} onRetry={load} />;

  return (
    <section>
      <div className="page-heading"><div><p className="kicker">Customer details</p><h1>Your profile</h1><p>Manage your account details and delivery addresses.</p></div></div>
      {notice && <div className="notice" role="status"><span>{notice}</span><button type="button" className="text-button" onClick={() => setNotice('')}>Dismiss</button></div>}
      {error && <div className="inline-error" role="alert">{error}</div>}
      <div className="profile-layout">
        <section className="surface-card">
          <div className="section-heading"><div><p className="kicker">Account</p><h2>Personal information</h2></div><span>Member since {new Date(profile.memberSince).toLocaleDateString()}</span></div>
          <form className="profile-form" onSubmit={saveProfile}>
            <label>First name<input value={profileForm.firstName} maxLength="50" onChange={(event) => setProfileForm((current) => ({ ...current, firstName: event.target.value }))} required /></label>
            <label>Last name<input value={profileForm.lastName} maxLength="50" onChange={(event) => setProfileForm((current) => ({ ...current, lastName: event.target.value }))} required /></label>
            <label className="field-wide">Email address<input type="email" value={profileForm.email} maxLength="320" onChange={(event) => setProfileForm((current) => ({ ...current, email: event.target.value }))} required /></label>
            <div className="form-actions field-wide"><button type="submit" className="button button--primary" disabled={submitting}>{submitting ? 'Saving…' : 'Save profile'}</button></div>
          </form>
        </section>

        <section className="surface-card addresses-section">
          <div className="section-heading"><div><p className="kicker">Delivery</p><h2>Saved addresses</h2></div><button type="button" className="button button--secondary" onClick={() => { setEditingAddress(null); setShowAddressForm(true); }}>Add address</button></div>
          {showAddressForm && <AddressForm key={editingAddress?.id || 'new'} initialValue={editingAddress} onSave={saveAddress} onCancel={() => { setShowAddressForm(false); setEditingAddress(null); }} submitting={submitting} />}
          {!showAddressForm && addresses.length === 0 && <EmptyState title="No saved addresses" message="Add a delivery address to make future orders easier." />}
          {!showAddressForm && addresses.length > 0 && <div className="address-grid">{addresses.map((address) => <article className={`address-card ${address.isDefault ? 'address-card--default' : ''}`} key={address.id}><div className="eyebrow-row"><span>{address.label}</span>{address.isDefault && <span className="default-badge">Default</span>}</div><address>{address.addressLine1}<br />{address.addressLine2 && <>{address.addressLine2}<br /></>}{address.city}{address.province ? `, ${address.province}` : ''} {address.postalCode}<br />{address.country}</address><div className="card-actions">{!address.isDefault && <button type="button" className="text-button" disabled={submitting} onClick={() => makeDefault(address)}>Make default</button>}<button type="button" className="text-button" onClick={() => { setEditingAddress(address); setShowAddressForm(true); }}>Edit</button><button type="button" className="text-button text-button--danger" disabled={submitting} onClick={() => removeAddress(address.id)}>Delete</button></div></article>)}</div>}
        </section>
      </div>
    </section>
  );
}
