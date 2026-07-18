import { Component } from '@angular/core';
import { MatSidenavModule } from '@angular/material/sidenav';

/**
 * Persistent app shell: a sidenav region (the sidebar nav is wired in by
 * SidebarNavComponent — see US2) and a content region bounded by the
 * design system's --content-max-width token. Rendered once in app.html
 * for authenticated routes only (FR-001); login/register stay outside it.
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [MatSidenavModule],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class AppShellComponent {}
