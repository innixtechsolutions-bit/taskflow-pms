import { Component, computed, effect, inject, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { TABLET_BREAKPOINT_QUERY } from '../breakpoints';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';
import { isNavItemVisible, NAV_ITEMS } from './nav-items';

const MANUAL_COLLAPSE_KEY = 'taskflow.sidebar-collapsed';

// sessionStorage access is a system boundary (can throw in e.g. locked-down
// privacy modes) — guarded here so a manual preference is a nice-to-have,
// never a page-breaking read/write.
function readStoredCollapsePreference(): boolean {
  try {
    return sessionStorage.getItem(MANUAL_COLLAPSE_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeStoredCollapsePreference(value: boolean): void {
  try {
    sessionStorage.setItem(MANUAL_COLLAPSE_KEY, String(value));
  } catch {
    // Nothing to fall back to — the toggle still works for the rest of
    // this page's lifetime via the in-memory signal, it just won't persist.
  }
}

/**
 * Sidebar navigation: product nav links (filtered by role), the signed-in
 * user block, and a logout trigger. Collapses to an icon rail below the
 * tablet breakpoint — CDK's BreakpointObserver, converted to a signal via
 * toSignal(), drives that without a manual subscription/unsubscribe — or
 * when the user manually collapses it via the hamburger toggle next to the
 * logo, a preference kept in sessionStorage for the rest of the browser
 * session (Feature 005 Polish, round 2).
 */
@Component({
  selector: 'app-sidebar-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, MatIconModule, MatMenuModule, MatTooltipModule, UserAvatarComponent],
  templateUrl: './sidebar-nav.component.html',
  styleUrl: './sidebar-nav.component.css',
})
export class SidebarNavComponent {
  protected readonly authService = inject(AuthService);
  private readonly breakpointObserver = inject(BreakpointObserver);

  readonly logout = output<void>();
  // Lets the shell size the sidenav container itself — this component only
  // owns whether the rail is collapsed, not how wide the host element is.
  readonly collapsedChange = output<boolean>();

  protected readonly navItems = computed(() =>
    NAV_ITEMS.filter((item) => isNavItemVisible(item, this.authService.currentRole()))
  );

  // BreakpointObserver.observe() emits synchronously on subscribe, so
  // requireSync: true is safe here and avoids an undefined/boolean union.
  private readonly breakpointState = toSignal(
    this.breakpointObserver.observe(TABLET_BREAKPOINT_QUERY),
    { requireSync: true }
  );

  private readonly manuallyCollapsed = signal(readStoredCollapsePreference());

  // The breakpoint's auto-collapse always wins — the manual toggle can only
  // add a collapse, never force an expand below the tablet width.
  protected readonly isCollapsed = computed(() => this.breakpointState().matches || this.manuallyCollapsed());

  constructor() {
    effect(() => this.collapsedChange.emit(this.isCollapsed()));
  }

  protected toggleCollapsed(): void {
    const next = !this.manuallyCollapsed();
    this.manuallyCollapsed.set(next);
    writeStoredCollapsePreference(next);
  }

  protected onLogoutClicked(): void {
    this.logout.emit();
  }
}
