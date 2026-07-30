import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { Login } from './features/auth/login/login';
import { Register } from './features/auth/register/register';
import { Home } from './features/home/home';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  { path: '', component: Home, canActivate: [authGuard] }
];
