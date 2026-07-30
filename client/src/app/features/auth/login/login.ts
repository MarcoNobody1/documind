import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth.service';

/**
 * Login UI. Deliberately minimal/throwaway (see portfolio/documind-ui-future), matching the
 * existing unstyled convention — a dedicated design slice follows.
 */
@Component({
  selector: 'app-login',
  imports: [RouterLink],
  templateUrl: './login.html'
})
export class Login {
  readonly email = signal('');
  readonly password = signal('');
  readonly isSubmitting = signal(false);
  readonly lastMessage = signal<string | null>(null);

  constructor(
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  onEmailInput(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
  }

  onPasswordInput(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
  }

  async submit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);
    this.lastMessage.set(null);

    try {
      await this.authService.login(this.email(), this.password());
      await this.router.navigateByUrl('/');
    } catch {
      this.lastMessage.set('Invalid email or password.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
