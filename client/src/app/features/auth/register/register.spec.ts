import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from '../../../core/auth.service';
import { Register } from './register';

describe('Register', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Register],
      providers: [provideHttpClient(), provideRouter([])]
    });
  });

  it('registers and navigates to / on success', async () => {
    const fixture = TestBed.createComponent(Register);
    const component = fixture.componentInstance;
    const authService = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    const registerSpy = vi.spyOn(authService, 'register').mockResolvedValue(undefined);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    component.email.set('demo@example.com');
    component.password.set('Sup3rSecret!23');
    await component.submit();

    expect(registerSpy).toHaveBeenCalledWith('demo@example.com', 'Sup3rSecret!23');
    expect(navigateSpy).toHaveBeenCalledWith('/');
    expect(component.lastMessage()).toBeNull();
  });

  it('shows a message and does not navigate on failure', async () => {
    const fixture = TestBed.createComponent(Register);
    const component = fixture.componentInstance;
    const authService = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    vi.spyOn(authService, 'register').mockRejectedValue(new Error('400'));
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    await component.submit();

    expect(component.lastMessage()).toBe('Could not register with that email and password.');
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
