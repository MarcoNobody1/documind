import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from '../../../core/auth.service';
import { Login } from './login';

describe('Login', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideRouter([])]
    });
  });

  it('logs in and navigates to / on success', async () => {
    const fixture = TestBed.createComponent(Login);
    const component = fixture.componentInstance;
    const authService = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    const loginSpy = vi.spyOn(authService, 'login').mockResolvedValue(undefined);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    component.email.set('demo@example.com');
    component.password.set('Sup3rSecret!23');
    await component.submit();

    expect(loginSpy).toHaveBeenCalledWith('demo@example.com', 'Sup3rSecret!23');
    expect(navigateSpy).toHaveBeenCalledWith('/');
    expect(component.lastMessage()).toBeNull();
  });

  it('shows a message and does not navigate on failure', async () => {
    const fixture = TestBed.createComponent(Login);
    const component = fixture.componentInstance;
    const authService = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    vi.spyOn(authService, 'login').mockRejectedValue(new Error('401'));
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    await component.submit();

    expect(component.lastMessage()).toBe('Invalid email or password.');
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
