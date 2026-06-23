import { HttpErrorResponse } from '@angular/common/http';
import { apiErrorMessage } from './api-error.util';
import { VEHICLE_UPLOAD_SIZE_ERROR } from './upload-url.util';

describe('apiErrorMessage', () => {
  it('returns plain text 400 body', () => {
    const error = new HttpErrorResponse({ status: 400, error: 'Registration number already exists.' });
    expect(apiErrorMessage(error, 'Fallback')).toBe('Registration number already exists.');
  });

  it('returns envelope message', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { message: 'Vehicle not found.' }
    });
    expect(apiErrorMessage(error, 'Fallback')).toBe('Vehicle not found.');
  });

  it('normalizes upload size errors', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { message: 'File exceeds maximum size of 2 MB.' }
    });
    expect(apiErrorMessage(error, 'Fallback')).toBe(VEHICLE_UPLOAD_SIZE_ERROR);
  });
});
