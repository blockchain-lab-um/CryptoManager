import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';
import { adminGuard } from './auth/admin.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: '',
    loadComponent: () => import('./auth/layout/auth-layout')
      .then(m => m.AuthLayout),
    children: [
      {
        path: 'login',
        loadComponent: () => import('./auth/login/login')
          .then(m => m.Login),
      },
      {
        path: 'register',
        loadComponent: () => import('./auth/register/register')
          .then(m => m.Register),
      },
    ],
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard')
      .then(m => m.DashboardPage),
  },
  {
    path: 'keys',
    canActivate: [authGuard],
    loadComponent: () => import('./features/keys/keys')
      .then(m => m.KeysPage),
  },
  {
    path: 'sign',
    canActivate: [authGuard],
    loadComponent: () => import('./features/sign/sign')
      .then(m => m.SignPage),
  },
  {
    path: 'verify',
    canActivate: [authGuard],
    loadComponent: () => import('./features/verify/verify')
      .then(m => m.VerifyPage),
  },
  {
    path: 'users',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/users/users')
      .then(m => m.UsersPage),
  },
];
