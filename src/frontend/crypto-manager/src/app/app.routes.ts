import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/dashboard/dashboard.component/dashboard.component')
      .then(m => m.DashboardComponent),
  },
  {
    path: 'keys',
    loadComponent: () => import('./pages/keys/keys.component/keys.component')
      .then(m => m.KeysComponent),
  },
  {
    path: 'sign',
    loadComponent: () => import('./pages/sign/sign.component/sign.component')
      .then(m => m.SignComponent),
  },
];
