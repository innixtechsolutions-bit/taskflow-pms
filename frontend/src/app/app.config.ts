import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Async variant: lazy-loads the animations engine on demand rather than
    // eagerly at bootstrap, which pairs better with zoneless change detection
    // than the classic provideAnimations().
    provideAnimationsAsync(),
    // MatDatepicker needs a DateAdapter — native Date is sufficient, no
    // extra date library needed (work-item-modal's date fields).
    provideNativeDateAdapter(),
  ]
};
