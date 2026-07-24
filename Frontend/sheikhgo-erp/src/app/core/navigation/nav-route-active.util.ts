/** Minimal nav shape used for longest-route active matching. */
export interface NavRouteCandidate {
  id: string;
  route: string;
  queryParams?: Record<string, string>;
}

/**
 * Returns true when `item` is the best (longest route) match for `normalizedPath`
 * among `allItems`. Query-param items require an exact path match plus params.
 */
export function isNavItemActive(
  item: NavRouteCandidate,
  normalizedPath: string,
  queryParams: Record<string, string | undefined>,
  allItems: readonly NavRouteCandidate[],
  aliasItemIds: ReadonlySet<string> = new Set()
): boolean {
  if (aliasItemIds.has(item.id)) return false;

  if (item.queryParams) {
    const onRoute = normalizedPath === item.route;
    return (
      onRoute &&
      Object.entries(item.queryParams).every(
        ([key, value]) => queryParams[key] === value
      )
    );
  }

  if (!item.route) return false;
  const onRoute =
    normalizedPath === item.route ||
    normalizedPath.startsWith(item.route + '/');
  if (!onRoute) return false;

  const betterMatch = allItems.some(other => {
    if (other.id === item.id) return false;
    if (!other.route || other.queryParams) return false;
    if (aliasItemIds.has(other.id)) return false;
    const otherMatches =
      normalizedPath === other.route ||
      normalizedPath.startsWith(other.route + '/');
    if (!otherMatches) return false;
    return other.route.length > item.route.length;
  });

  return !betterMatch;
}
