import { UserRole } from '../auth/auth.service';

// The exact rule already enforced server-side in WorkItemService.EnsureCanEdit
// (research.md #3/#5) — extracted here since the board is this rule's third
// frontend call site (alongside project-detail and work-item-detail), a
// justified consolidation rather than premature abstraction.
export function canEditWorkItem(
  item: { createdByUserId: number; assigneeUserId: number | null },
  currentUserId: number,
  currentRole: UserRole | null
): boolean {
  return (
    item.createdByUserId === currentUserId ||
    item.assigneeUserId === currentUserId ||
    currentRole === 'Manager' ||
    currentRole === 'Admin'
  );
}
