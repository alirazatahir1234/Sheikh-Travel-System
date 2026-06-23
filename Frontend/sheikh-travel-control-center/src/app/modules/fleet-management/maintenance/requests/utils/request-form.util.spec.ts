import {
  defaultCreateMaintenanceRequestForm,
  DESCRIPTION_MAX_LENGTH,
  DESCRIPTION_MIN_LENGTH,
  validateCreateMaintenanceRequest
} from './request-form.util';

describe('request-form.util', () => {
  const validForm = () => ({
    ...defaultCreateMaintenanceRequestForm(),
    vehicleId: 1,
    priority: 'Medium' as const,
    issueCategory: 'Engine',
    requestType: 'Corrective',
    description: 'Engine overheating on highway drive.'
  });

  it('accepts a valid create payload', () => {
    const result = validateCreateMaintenanceRequest(validForm());
    expect(result.valid).toBeTrue();
    expect(result.errors).toEqual({});
  });

  it('requires vehicle selection', () => {
    const result = validateCreateMaintenanceRequest({ ...validForm(), vehicleId: 0 });
    expect(result.valid).toBeFalse();
    expect(result.errors.vehicleId).toBe('Please select a vehicle.');
  });

  it('requires priority, category, and type', () => {
    const result = validateCreateMaintenanceRequest({
      ...defaultCreateMaintenanceRequestForm(),
      vehicleId: 1,
      description: 'Valid description text.'
    });
    expect(result.valid).toBeFalse();
    expect(result.errors.priority).toBe('Priority is required.');
    expect(result.errors.issueCategory).toBe('Category is required.');
    expect(result.errors.requestType).toBe('Type is required.');
  });

  it('enforces description length bounds', () => {
    const tooShort = validateCreateMaintenanceRequest({
      ...validForm(),
      description: 'short'
    });
    expect(tooShort.valid).toBeFalse();
    expect(tooShort.errors.description).toBe(`Description must be at least ${DESCRIPTION_MIN_LENGTH} characters.`);

    const tooLong = validateCreateMaintenanceRequest({
      ...validForm(),
      description: 'x'.repeat(DESCRIPTION_MAX_LENGTH + 1)
    });
    expect(tooLong.valid).toBeFalse();
    expect(tooLong.errors.description).toBe(`Description cannot exceed ${DESCRIPTION_MAX_LENGTH} characters.`);
  });

  it('rejects whitespace-only description', () => {
    const result = validateCreateMaintenanceRequest({
      ...validForm(),
      description: '          '
    });
    expect(result.valid).toBeFalse();
    expect(result.errors.description).toBe('Description is required.');
  });
});
