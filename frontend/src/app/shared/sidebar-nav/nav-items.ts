import { UserRole } from '../../auth/auth.service';

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  visibleTo: 'all' | UserRole[];
}

// Static, code-owned nav config (research.md #9) — no backend endpoint;
// future features add their own entries here.
export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: 'dashboard', route: '/', visibleTo: 'all' },
  { label: 'Projects', icon: 'folder', route: '/projects', visibleTo: 'all' },
  { label: 'Users', icon: 'group', route: '/users', visibleTo: ['Admin'] },
];

export function isNavItemVisible(item: NavItem, role: UserRole | null): boolean {
  if (item.visibleTo === 'all') {
    return true;
  }
  return role !== null && item.visibleTo.includes(role);
}
