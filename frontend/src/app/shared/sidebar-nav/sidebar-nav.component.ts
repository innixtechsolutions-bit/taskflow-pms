import { Component, computed, inject, output } from '@angular/core';
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

/**
 * Sidebar navigation: product nav links (filtered by role), the signed-in
 * user block, and a logout trigger. Collapses to an icon rail below the
 * tablet breakpoint — CDK's BreakpointObserver, converted to a signal via
 * toSignal(), drives that without a manual subscription/unsubscribe.
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

  protected readonly navItems = computed(() =>
    NAV_ITEMS.filter((item) => isNavItemVisible(item, this.authService.currentRole()))
  );

  // BreakpointObserver.observe() emits synchronously on subscribe, so
  // requireSync: true is safe here and avoids an undefined/boolean union.
  private readonly breakpointState = toSignal(
    this.breakpointObserver.observe(TABLET_BREAKPOINT_QUERY),
    { requireSync: true }
  );

  protected readonly isCollapsed = computed(() => this.breakpointState().matches);

  protected onLogoutClicked(): void {
    this.logout.emit();
  }
}
