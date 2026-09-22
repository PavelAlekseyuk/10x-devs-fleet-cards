import { Routes } from '@angular/router';
import { LoginComponent } from './login.component';
import { ShellComponent } from './shell.component';
import { authGuard, readWriteGuard } from './auth.guards';

export const routes: Routes = [
	{ path: 'login', component: LoginComponent },
	{ path: 'overview', component: ShellComponent, data: { section: 'overview' }, canMatch: [authGuard] },
	{ path: 'cars', component: ShellComponent, data: { section: 'cars' }, canMatch: [authGuard] },
	{ path: 'cards', component: ShellComponent, data: { section: 'cards' }, canMatch: [authGuard] },
	{ path: 'transactions', component: ShellComponent, data: { section: 'transactions' }, canMatch: [authGuard] },
	{ path: 'programs', component: ShellComponent, data: { section: 'programs' }, canMatch: [authGuard, readWriteGuard] },
	{ path: '', pathMatch: 'full', redirectTo: 'overview' },
	{ path: '**', redirectTo: 'overview' },
];
