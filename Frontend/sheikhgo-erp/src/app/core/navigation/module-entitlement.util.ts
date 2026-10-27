/** Whether any required legacy module key is enabled for the tenant. */
export function isAnyModuleEntitled(
  enabledModules: readonly string[] | undefined,
  requiredModules: readonly string[] | undefined
): boolean {
  const required = requiredModules ?? [];
  if (!required.length) return true;

  const enabled = enabledModules ?? [];
  if (!enabled.length) return true;

  return required.some(requiredKey =>
    enabled.some(e => e.toLowerCase() === requiredKey.toLowerCase())
  );
}

/** Bookings & Trips share the BOOKINGS product module (legacy keys bookings + trips). */
export function travelOperationsModuleKeys(): readonly string[] {
  return ['bookings', 'trips'];
}
