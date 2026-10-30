import { Routes } from '@angular/router';
import { Dashboard } from './screens/dashboard/dashboard';
import { LogEntry } from './screens/log-entry/log-entry';
import { Setup } from './screens/setup/setup';
import { Trend } from './screens/trend/trend';

export const routes: Routes = [
    { path: 'dashboard', component: Dashboard },
    { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    { path: 'log', component: LogEntry },
    { path: 'setup', component: Setup },
    { path: 'trend', component: Trend },
    { path: '**', redirectTo: 'dashboard' },
];

