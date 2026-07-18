import { Component, inject } from '@angular/core';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Router } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { SidebarNavComponent } from '../sidebar-nav/sidebar-nav.component';

/**
 * Persistent app shell: sidenav (SidebarNavComponent) + a content region
 * bounded by the design system's --content-max-width token. Rendered once
 * in app.html for authenticated routes only (FR-001); login/register stay
 * outside it.
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [MatSidenavModule, SidebarNavComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class AppShellComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected async onLogout(): Promise<void> {
    await this.authService.logout();
    await this.router.navigateByUrl('/login');
  }
}
