import { canEditWorkItem } from './work-item-permissions';

const item = { createdByUserId: 10, assigneeUserId: 20 };

describe('canEditWorkItem', () => {
  it('is true for the creator', () => {
    expect(canEditWorkItem(item, 10, 'Developer')).toBe(true);
  });

  it('is true for the current assignee', () => {
    expect(canEditWorkItem(item, 20, 'Developer')).toBe(true);
  });

  it('is true for a Manager who is neither creator nor assignee', () => {
    expect(canEditWorkItem(item, 99, 'Manager')).toBe(true);
  });

  it('is true for an Admin who is neither creator nor assignee', () => {
    expect(canEditWorkItem(item, 99, 'Admin')).toBe(true);
  });

  it('is false for an unrelated Developer', () => {
    expect(canEditWorkItem(item, 99, 'Developer')).toBe(false);
  });

  it('is false when there is no signed-in role', () => {
    expect(canEditWorkItem(item, 99, null)).toBe(false);
  });
});
