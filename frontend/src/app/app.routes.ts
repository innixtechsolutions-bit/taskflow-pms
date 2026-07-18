import { Routes } from '@angular/router';
import { adminGuard } from './auth/admin.guard';
import { authGuard } from './auth/auth.guard';
import { LoginComponent } from './auth/login/login.component';
import { RegisterComponent } from './auth/register/register.component';
import { HomeComponent } from './home/home.component';
import { ProjectDetailComponent } from './projects/project-detail/project-detail.component';
import { ProjectFormComponent } from './projects/project-form/project-form.component';
import { ProjectsListComponent } from './projects/projects-list/projects-list.component';
import { WorkItemFormComponent } from './projects/work-item-form/work-item-form.component';
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
  { path: 'projects/:projectId/work-items/new', component: WorkItemFormComponent, canActivate: [authGuard] },
  { path: 'projects/:projectId/work-items/:id/edit', component: WorkItemFormComponent, canActivate: [authGuard] },
  { path: 'projects/:id/edit', component: ProjectFormComponent, canActivate: [authGuard] },
  { path: 'projects/:id', component: ProjectDetailComponent, canActivate: [authGuard] },
];
