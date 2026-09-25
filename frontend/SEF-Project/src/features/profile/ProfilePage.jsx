import { useCallback, useEffect, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { formatDate } from '../../utils/format';
import { AddressForm } from './AddressForm';
import {
  createAddress,
  deleteAddress,
  getAddresses,
  getProfile,
  updateAddress,
  updateProfile,
} from './profileService';

export function ProfilePage() {
  const { token } = useAuth();

  const [profile, setProfile] = useState(null);
  const [profileForm, setProfileForm] = useState({
    email: '',
    firstName: '',
    lastName: '',
  });
  const [addresses, setAddresses] = useState([]);
  const [editingAddress, setEditingAddress] = useState(null);
  const [isFormVisible, setIsFormVisible] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);

  const loadProfile = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [profileResponse, addressResponse] = await Promise.all([
        getProfile(token),
        getAddresses(token),
      ]);

      setProfile(profileResponse);
      setProfileForm({
        email: profileResponse.email,
        firstName: profileResponse.firstName,
        lastName: profileResponse.lastName,
      });
      setAddresses(addressResponse);
    } catch (requestError) {
      setError(requestError?.message || 'Your profile could not be loaded.');
    } finally {
      setIsLoading(false);
    }
  }, [token]);

  useEffect(() => {
    // This effect intentionally loads the signed-in customer's profile.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadProfile();
  }, [loadProfile]);

  async function saveProfile(event) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setNotice(null);

    try {
      const response = await updateProfile(token, {
        email: profileForm.email.trim(),
        firstName: profileForm.firstName.trim(),
        lastName: profileForm.lastName.trim(),
      });

      setProfile(response);
      setProfileForm({
        email: response.email,
        firstName: response.firstName,
        lastName: response.lastName,
      });
      setNotice('Profile updated.');
    } catch (requestError) {
      setError(requestError?.message || 'Your profile could not be saved.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function saveAddress(address) {
    setIsSubmitting(true);
    setError(null);
    setNotice(null);

    try {
      if (editingAddress) {
        await updateAddress(token, editingAddress.id, address);
      } else {
        await createAddress(token, address);
      }

      setEditingAddress(null);
      setIsFormVisible(false);
      setAddresses(await getAddresses(token));
      setNotice(editingAddress ? 'Address updated.' : 'Address added.');
    } catch (requestError) {
      setError(requestError?.message || 'The address could not be saved.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function removeAddress(addressId) {
    setIsSubmitting(true);
    setError(null);
    setNotice(null);

    try {
      await deleteAddress(token, addressId);
      setAddresses(await getAddresses(token));
      setNotice('Address removed.');
    } catch (requestError) {
      setError(requestError?.message || 'The address could not be removed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function makeDefaultAddress(address) {
    setIsSubmitting(true);
    setError(null);
    setNotice(null);

    try {
      await updateAddress(token, address.id, {
        label: address.label,
        addressLine1: address.addressLine1,
        addressLine2: address.addressLine2 ?? '',
        city: address.city,
        province: address.province ?? '',
        postalCode: address.postalCode,
        country: address.country,
        isDefault: true,
      });

      setAddresses(await getAddresses(token));
      setNotice('Default address updated.');
    } catch (requestError) {
      setError(requestError?.message || 'The default address could not be changed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isLoading) {
    return (
      <PageShell eyebrow="Clothic · Account" title="Your profile">
        <LoadingState message="Loading your profile..." />
      </PageShell>
    );
  }

  if (!profile) {
    return (
      <PageShell eyebrow="Clothic · Account" title="Your profile">
        <ApiErrorAlert message={error} onRetry={loadProfile} />
      </PageShell>
    );
  }

  return (
    <PageShell
      description="Manage your account details and the addresses we deliver to."
      eyebrow="Clothic · Account"
      title="Your profile"
    >
      {notice && <Alert>{notice}</Alert>}

      <ApiErrorAlert message={error} />

      <div className="profile-layout">
        <section className="surface-card">
          <div className="section-heading">
            <div>
              <p className="kicker">Account</p>
              <h2>Personal information</h2>
            </div>
            <span>Member since {formatDate(profile.memberSince)}</span>
          </div>

          <form className="profile-form" onSubmit={saveProfile}>
            <label>
              First name
              <input
                maxLength={50}
                onChange={(event) => setProfileForm((current) => ({
                  ...current,
                  firstName: event.target.value,
                }))}
                required
                value={profileForm.firstName}
              />
            </label>

            <label>
              Last name
              <input
                maxLength={50}
                onChange={(event) => setProfileForm((current) => ({
                  ...current,
                  lastName: event.target.value,
                }))}
                required
                value={profileForm.lastName}
              />
            </label>

            <label className="field-wide">
              Email address
              <input
                maxLength={320}
                onChange={(event) => setProfileForm((current) => ({
                  ...current,
                  email: event.target.value,
                }))}
                required
                type="email"
                value={profileForm.email}
              />
            </label>

            <div className="form-actions field-wide">
              <button disabled={isSubmitting} type="submit">
                {isSubmitting ? 'Saving...' : 'Save profile'}
              </button>
            </div>
          </form>
        </section>

        <section className="surface-card">
          <div className="section-heading">
            <div>
              <p className="kicker">Delivery</p>
              <h2>Saved addresses</h2>
            </div>
            <button
              className="button-secondary"
              onClick={() => {
                setEditingAddress(null);
                setIsFormVisible(true);
              }}
              type="button"
            >
              Add address
            </button>
          </div>

          {isFormVisible && (
            <AddressForm
              initialValue={editingAddress}
              isSubmitting={isSubmitting}
              key={editingAddress?.id ?? 'new'}
              onCancel={() => {
                setIsFormVisible(false);
                setEditingAddress(null);
              }}
              onSave={saveAddress}
            />
          )}

          {!isFormVisible && addresses.length === 0 && (
            <div className="empty-state">
              No saved addresses yet. Add one to speed up future orders.
            </div>
          )}

          {!isFormVisible && addresses.length > 0 && (
            <div className="address-grid">
              {addresses.map((address) => (
                <article
                  className={`address-card${address.isDefault ? ' address-card--default' : ''}`}
                  key={address.id}
                >
                  <div className="eyebrow-row">
                    <span>{address.label}</span>
                    {address.isDefault && <span className="default-badge">Default</span>}
                  </div>

                  <address>
                    {address.addressLine1}
                    <br />
                    {address.addressLine2 && (
                      <>
                        {address.addressLine2}
                        <br />
                      </>
                    )}
                    {address.city}
                    {address.province ? `, ${address.province}` : ''} {address.postalCode}
                    <br />
                    {address.country}
                  </address>

                  <div className="card-actions">
                    {!address.isDefault && (
                      <button
                        className="button-secondary"
                        disabled={isSubmitting}
                        onClick={() => makeDefaultAddress(address)}
                        type="button"
                      >
                        Make default
                      </button>
                    )}
                    <button
                      className="button-secondary"
                      onClick={() => {
                        setEditingAddress(address);
                        setIsFormVisible(true);
                      }}
                      type="button"
                    >
                      Edit
                    </button>
                    <button
                      className="button-secondary"
                      disabled={isSubmitting}
                      onClick={() => removeAddress(address.id)}
                      type="button"
                    >
                      Delete
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
    </PageShell>
  );
}

export default ProfilePage;
