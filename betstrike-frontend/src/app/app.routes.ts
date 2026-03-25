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
    path: 'admin/games',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/admin-games.component').then((m) => m.AdminGamesComponent)
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];
