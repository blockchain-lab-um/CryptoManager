import { Routes } from '@angular/router';
import { DashboardComponent } from './pages/dashboard/dashboard.component/dashboard.component';
import { KeysComponent } from './pages/keys/keys.component/keys.component';
import { SignComponent } from './pages/sign/sign.component/sign.component';

export const routes: Routes = [
    { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    { path: 'dashboard', component: DashboardComponent },
    { path: 'keys', component: KeysComponent },
    { path: 'sign', component: SignComponent },
];
