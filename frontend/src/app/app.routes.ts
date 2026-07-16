import { Routes } from '@angular/router';
import { RegisterComponent } from './auth/register/register.component';

// No wildcard route yet: US2 (login) and later stories will add more real
// destinations, and a catch-all added now would silently swallow typos in
// those paths during development instead of surfacing a clear 404.
export const routes: Routes = [
  { path: 'register', component: RegisterComponent },
  { path: '', redirectTo: '/register', pathMatch: 'full' },
];
