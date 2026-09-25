import { describe, expect, it } from 'vitest';
import { toErrorMessage, toFieldErrors } from './apiErrors';

describe('toFieldErrors', () => {
  it('maps ASP.NET Core validation errors to camelCase fields', () => {
    const error = {
      status: 400,
      data: {
        errors: {
          Name: ['Name is required.'],
          DiscountValue: ['Too large.', 'Second message ignored.'],
          '$.type': ['The JSON value could not be converted.'],
        },
      },
    };

    expect(toFieldErrors(error)).toEqual({
      name: 'Name is required.',
      discountValue: 'Too large.',
      type: 'The JSON value could not be converted.',
    });
  });

  it('returns an empty object when there are no field errors', () => {
    expect(toFieldErrors({ status: 409, data: { detail: 'Conflict' } })).toEqual({});
    expect(toFieldErrors(undefined)).toEqual({});
  });
});

describe('toErrorMessage', () => {
  it.each([
    [{ status: 401 }, 'Your session has expired. Please log in again.'],
    [{ status: 403 }, 'You do not have permission to do this.'],
    [{ status: 404 }, 'The requested item was not found.'],
    [{ status: 400, data: { errors: { Name: ['x'] } } }, 'Please correct the highlighted fields.'],
    [{ status: 409, message: 'Campaign has promotions.' }, 'Campaign has promotions.'],
  ])('describes %o', (error, expected) => {
    expect(toErrorMessage(error)).toBe(expected);
  });
});
