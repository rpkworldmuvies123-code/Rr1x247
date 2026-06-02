import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'login'
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register.component').then((m) => m.RegisterComponent)
  },
  {
    path: 'games',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/games/games-list.component').then((m) => m.GamesListComponent)
  },
  {
    path: 'games/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/games/game-details.component').then((m) => m.GameDetailsComponent)
  },
  {
    path: 'games/:id/bet',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/bets/game-bet.component').then((m) => m.GameBetComponent)
  },
  {
    path: 'bets',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/bets/my-bets.component').then((m) => m.MyBetsComponent)
  },
  {
    path: 'admin/games',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/admin-games.component').then((m) => m.AdminGamesComponent)
  },
  {
    path: 'admin/users',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/admin-users.component').then((m) => m.AdminUsersComponent)
  },
  {
    path: 'admin/analytics',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/analytics-dashboard.component').then((m) => m.AnalyticsDashboardComponent)
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];
