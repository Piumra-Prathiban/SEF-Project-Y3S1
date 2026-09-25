export const emptyAddress = {
  label: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  province: '',
  postalCode: '',
  country: 'Sri Lanka',
  isDefault: false,
};

export function toAddressRequest(address) {
  return {
    label: address.label.trim(),
    addressLine1: address.addressLine1.trim(),
    addressLine2: address.addressLine2?.trim() ?? '',
    city: address.city.trim(),
    province: address.province?.trim() ?? '',
    postalCode: address.postalCode.trim(),
    country: address.country.trim(),
    isDefault: Boolean(address.isDefault),
  };
}

export function validateAddress(address) {
  const errors = [];
  const required = ['label', 'addressLine1', 'city', 'postalCode', 'country'];

  if (required.some((field) => !address[field]?.trim())) {
    errors.push('Please complete every required address field.');
  }

  if (!/^[A-Za-z0-9][A-Za-z0-9 -]*$/.test(address.postalCode?.trim() ?? '')) {
    errors.push('Enter a valid postal code.');
  }

  return errors;
}
