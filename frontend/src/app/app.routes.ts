import { Routes } from '@angular/router';
import { adminGuard } from './auth/admin.guard';
import { authGuard } from './auth/auth.guard';

// Every route below is lazy (`loadComponent`), not a static `component:`
// reference — each routed component (and everything only it imports, e.g.
// Angular Material modules, the work item modal) ships in its own chunk,
// fetched on navigation instead of bundled into the eagerly-loaded main
// chunk every visitor downloads before ever reaching a route (fix: restore
// production build). Standalone components support this natively; no
// NgModule wrapping is needed.

// No wildcard route yet: later stories will add more real destinations, and a
// catch-all added now would silently swallow typos in those paths during
// development instead of surfacing a clear 404.
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home.component').then((m) => m.HomeComponent),
    canActivate: [authGuard],
  },
  { path: 'login', loadComponent: () => import('./auth/login/login.component').then((m) => m.LoginComponent) },
  {
    path: 'register',
    loadComponent: () => import('./auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'users',
    loadComponent: () => import('./users/users-list/users-list.component').then((m) => m.UsersListComponent),
    canActivate: [adminGuard],
  },
  // 'projects/new' must come before 'projects/:id' — route matching is
  // order-dependent, and the parameterized route would otherwise swallow it.
  {
    path: 'projects',
    loadComponent: () =>
      import('./projects/projects-list/projects-list.component').then((m) => m.ProjectsListComponent),
    canActivate: [authGuard],
  },
  {
    path: 'projects/new',
    loadComponent: () => import('./projects/project-form/project-form.component').then((m) => m.ProjectFormComponent),
    canActivate: [authGuard],
  },
  // US1 (Feature 007): the full-page create/edit forms are gone — every entry
  // point opens WorkItemModalComponent directly instead of navigating here.
  // These two redirects exist only so a stale bookmark/external link lands on
  // a working page rather than a dead one (research.md #10) — no modal
  // auto-opens on arrival.
  { path: 'projects/:projectId/work-items/new', redirectTo: 'projects/:projectId' },
  { path: 'projects/:projectId/work-items/:id/edit', redirectTo: 'projects/:projectId/work-items/:id' },
  {
    path: 'projects/:projectId/work-items/:id',
    loadComponent: () =>
      import('./projects/work-item-detail/work-item-detail.component').then((m) => m.WorkItemDetailComponent),
    canActivate: [authGuard],
  },
  {
    path: 'projects/:id/edit',
    loadComponent: () => import('./projects/project-form/project-form.component').then((m) => m.ProjectFormComponent),
    canActivate: [authGuard],
  },
  {
    path: 'projects/:id/workflow',
    loadComponent: () => import('./projects/workflow/workflow.component').then((m) => m.WorkflowComponent),
    canActivate: [authGuard],
  },
  {
    path: 'projects/:id',
    loadComponent: () =>
      import('./projects/project-detail/project-detail.component').then((m) => m.ProjectDetailComponent),
    canActivate: [authGuard],
  },
];
