import { Component, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { AuthService } from '../auth/auth.service';

// Minimal placeholder: this feature (001-user-auth) only needs somewhere real for
// login/register to land and for authGuard to protect — the actual TaskFlow home/
// dashboard content belongs to a later feature.
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [MatCardModule],
  templateUrl: './home.component.html',
})
export class HomeComponent {
  protected readonly authService = inject(AuthService);
}
