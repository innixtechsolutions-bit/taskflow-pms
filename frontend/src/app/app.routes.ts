import { Routes } from '@angular/router';
import { adminGuard } from './auth/admin.guard';
import { authGuard } from './auth/auth.guard';
import { LoginComponent } from './auth/login/login.component';
import { RegisterComponent } from './auth/register/register.component';
import { HomeComponent } from './home/home.component';
import { ProjectDetailComponent } from './projects/project-detail/project-detail.component';
import { ProjectFormComponent } from './projects/project-form/project-form.component';
import { ProjectsListComponent } from './projects/projects-list/projects-list.component';
import { WorkItemDetailComponent } from './projects/work-item-detail/work-item-detail.component';
import { WorkflowComponent } from './projects/workflow/workflow.component';
import { UsersListComponent } from './users/users-list/users-list.component';

// No wildcard route yet: later stories will add more real destinations, and a
// catch-all added now would silently swallow typos in those paths during
// development instead of surfacing a clear 404.
export const routes: Routes = [
  { path: '', component: HomeComponent, canActivate: [authGuard] },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'users', component: UsersListComponent, canActivate: [adminGuard] },
  // 'projects/new' must come before 'projects/:id' — route matching is
  // order-dependent, and the parameterized route would otherwise swallow it.
  { path: 'projects', component: ProjectsListComponent, canActivate: [authGuard] },
  { path: 'projects/new', component: ProjectFormComponent, canActivate: [authGuard] },
  // US1 (Feature 007): the full-page create/edit forms are gone — every entry
  // point opens WorkItemModalComponent directly instead of navigating here.
  // These two redirects exist only so a stale bookmark/external link lands on
  // a working page rather than a dead one (research.md #10) — no modal
  // auto-opens on arrival.
  { path: 'projects/:projectId/work-items/new', redirectTo: 'projects/:projectId' },
  { path: 'projects/:projectId/work-items/:id/edit', redirectTo: 'projects/:projectId/work-items/:id' },
  { path: 'projects/:projectId/work-items/:id', component: WorkItemDetailComponent, canActivate: [authGuard] },
  { path: 'projects/:id/edit', component: ProjectFormComponent, canActivate: [authGuard] },
  { path: 'projects/:id/workflow', component: WorkflowComponent, canActivate: [authGuard] },
  { path: 'projects/:id', component: ProjectDetailComponent, canActivate: [authGuard] },
];
