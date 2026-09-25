import { useState } from 'react';
import {
  emptyAddress,
  toAddressRequest,
  validateAddress,
} from './addressFormUtils';

export function AddressForm({ initialValue, isSubmitting, onCancel, onSave }) {
  const [address, setAddress] = useState(() => ({
    ...emptyAddress,
    ...initialValue,
  }));
  const [errors, setErrors] = useState([]);

  function setField(name, value) {
    setAddress((current) => ({ ...current, [name]: value }));
  }

  function handleSubmit(event) {
    event.preventDefault();

    const validationErrors = validateAddress(address);
    setErrors(validationErrors);

    if (validationErrors.length === 0) {
      onSave(toAddressRequest(address));
    }
  }

  return (
    <form className="address-form" onSubmit={handleSubmit}>
      {errors.length > 0 && (
        <ul className="error-list field-wide" role="alert">
          {errors.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}

      <label>
        Label
        <input
          maxLength={50}
          onChange={(event) => setField('label', event.target.value)}
          placeholder="Home"
          required
          value={address.label}
        />
      </label>

      <label className="field-wide">
        Address line 1
        <input
          maxLength={200}
          onChange={(event) => setField('addressLine1', event.target.value)}
          required
          value={address.addressLine1}
        />
      </label>

      <label className="field-wide">
        Address line 2 (optional)
        <input
          maxLength={200}
          onChange={(event) => setField('addressLine2', event.target.value)}
          value={address.addressLine2}
        />
      </label>

      <label>
        City
        <input
          maxLength={100}
          onChange={(event) => setField('city', event.target.value)}
          required
          value={address.city}
        />
      </label>

      <label>
        Province (optional)
        <input
          maxLength={100}
          onChange={(event) => setField('province', event.target.value)}
          value={address.province}
        />
      </label>

      <label>
        Postal code
        <input
          maxLength={20}
          minLength={2}
          onChange={(event) => setField('postalCode', event.target.value)}
          required
          value={address.postalCode}
        />
      </label>

      <label>
        Country
        <input
          maxLength={100}
          onChange={(event) => setField('country', event.target.value)}
          required
          value={address.country}
        />
      </label>

      <label className="checkbox-field field-wide">
        <input
          checked={address.isDefault}
          onChange={(event) => setField('isDefault', event.target.checked)}
          type="checkbox"
        />
        Use as my default address
      </label>

      <div className="form-actions field-wide">
        <button disabled={isSubmitting} type="submit">
          {isSubmitting ? 'Saving...' : 'Save address'}
        </button>
        <button
          className="button-secondary"
          onClick={onCancel}
          type="button"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

export default AddressForm;
