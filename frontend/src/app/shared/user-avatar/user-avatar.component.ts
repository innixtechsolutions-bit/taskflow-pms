import { Component, computed, input } from '@angular/core';

const AVATAR_PALETTE_SIZE = 8;

// Deterministic same-name -> same-color hash (research.md #5, revised):
// fullName is the only identifier present in every context this component
// renders in (tree view, flat list, detail, sidebar, Users list) — the
// tree endpoint's DTO has no user id, only a name.
export function avatarColorFor(fullName: string): string {
  let hash = 0;
  for (let i = 0; i < fullName.length; i++) {
    hash = (hash * 31 + fullName.charCodeAt(i)) | 0;
  }
  const index = (Math.abs(hash) % AVATAR_PALETTE_SIZE) + 1;
  return `var(--color-avatar-${index})`;
}

export function initialsFor(fullName: string): string {
  return fullName
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('');
}

/**
 * Circular initials avatar with a background color deterministically
 * derived from the person's name (FR-010) — identical everywhere the same
 * name is rendered.
 */
@Component({
  selector: 'app-user-avatar',
  standalone: true,
  templateUrl: './user-avatar.component.html',
  styleUrl: './user-avatar.component.css',
})
export class UserAvatarComponent {
  readonly fullName = input.required<string>();
  readonly showName = input(false);

  protected readonly initials = computed(() => initialsFor(this.fullName()));
  protected readonly color = computed(() => avatarColorFor(this.fullName()));
}
