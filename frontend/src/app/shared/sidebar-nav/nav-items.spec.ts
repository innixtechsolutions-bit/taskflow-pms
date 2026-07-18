import { NAV_ITEMS, isNavItemVisible } from './nav-items';

describe('nav-items', () => {
  it("shows Dashboard and Projects to every role, and Users only to Admin", () => {
    const dashboard = NAV_ITEMS.find((i) => i.label === 'Dashboard')!;
    const projects = NAV_ITEMS.find((i) => i.label === 'Projects')!;
    const users = NAV_ITEMS.find((i) => i.label === 'Users')!;

    for (const role of ['Developer', 'Manager', 'Admin'] as const) {
      expect(isNavItemVisible(dashboard, role)).toBe(true);
      expect(isNavItemVisible(projects, role)).toBe(true);
    }

    expect(isNavItemVisible(users, 'Admin')).toBe(true);
    expect(isNavItemVisible(users, 'Manager')).toBe(false);
    expect(isNavItemVisible(users, 'Developer')).toBe(false);
  });

  it('hides role-restricted items when there is no signed-in role', () => {
    const users = NAV_ITEMS.find((i) => i.label === 'Users')!;
    expect(isNavItemVisible(users, null)).toBe(false);
  });
});
