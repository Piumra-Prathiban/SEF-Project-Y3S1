const HEX_CODE_PATTERN = /^#[0-9a-f]{6}$/i;

export function validateColourForm(colour) {
  const errors = [];

  if (!colour.name?.trim()) {
    errors.push('Colour name is required.');
  }

  if (colour.name && colour.name.length > 100) {
    errors.push('Colour name must be 100 characters or fewer.');
  }

  if (colour.hexCode?.trim() && !HEX_CODE_PATTERN.test(colour.hexCode.trim())) {
    errors.push('Hex code must be a valid value such as #000000.');
  }

  return errors;
}

export function buildColourPayload(colour) {
  return {
    name: colour.name.trim(),
    hexCode: colour.hexCode?.trim() || null,
    isActive: colour.isActive,
  };
}

export function filterColours(colours, filters) {
  const search = filters.search.trim().toLowerCase();

  return colours.filter((colour) => {
    const matchesSearch =
      !search
      || colour.name.toLowerCase().includes(search)
      || colour.hexCode?.toLowerCase().includes(search);

    const matchesStatus =
      filters.isActive === ''
      || String(colour.isActive) === filters.isActive;

    return matchesSearch && matchesStatus;
  });
}

export function isValidHexCode(hexCode) {
  return HEX_CODE_PATTERN.test(hexCode);
}
