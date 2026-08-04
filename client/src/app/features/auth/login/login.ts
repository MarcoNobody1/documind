import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmLabelImports } from '@spartan-ng/helm/label';

import { AuthService } from '../../../core/auth.service';

/**
 * Login UI, restyled on the Helm primitives already in the repo (card, button) plus the two
 * pulled by this slice (input, label). Host declares `flex flex-1 items-center justify-center
 * p-6` per ADR-L: the shared `<main>` (PR3) is a full-viewport flex column with no centring of
 * its own, so the auth screens own their own centring rather than inheriting one.
 */
@Component({
  selector: 'app-login',
  imports: [RouterLink, ...HlmCardImports, ...HlmButtonImports, ...HlmInputImports, ...HlmLabelImports],
  templateUrl: './login.html',
  host: { class: 'flex flex-1 items-center justify-center p-6' }
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
