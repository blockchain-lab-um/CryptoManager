import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: '',
    loadComponent: () => import('./auth/layout/auth-layout.component')
      .then(m => m.AuthLayoutComponent),
    children: [
      {
        path: 'login',
        loadComponent: () => import('./auth/login/login.component')
          .then(m => m.LoginComponent),
      },
      {
        path: 'register',
        loadComponent: () => import('./auth/register/register.component')
          .then(m => m.RegisterComponent),
      },
    ],
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/dashboard/dashboard.component/dashboard.component')
      .then(m => m.DashboardComponent),
  },
  {
    path: 'keys',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/keys/keys.component/keys.component')
      .then(m => m.KeysComponent),
  },
  {
    path: 'sign',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/sign/sign.component/sign.component')
      .then(m => m.SignComponent),
  },
  {
    path: 'verify',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/verify/verify.component/verify.component')
      .then(m => m.VerifyComponent),
  },
];
