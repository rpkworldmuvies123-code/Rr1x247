import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
import { BackendApiService } from './core/api/backend-api.service';
import { GameDetails } from './core/models/game.model';
import { AdminGamesComponent } from './features/admin/admin-games.component';
import { LoginComponent } from './features/auth/login.component';
import { GameDetailsComponent } from './features/games/game-details.component';
import { GamesListComponent } from './features/games/games-list.component';
import { expectNoAxeViolations } from '../testing/axe-helpers';

const mockGameDetails: GameDetails = {
  id: 'game-1',
  code: 'BS001',
  homeTeam: 'Porto',
  awayTeam: 'Sporting',
  competition: 'Liga',
  startTime: '2026-03-25T20:00:00.000Z',
  status: 'LIVE',
  homeScore: 1,
  awayScore: 0,
  venue: 'Dragao',
  events: ['12\' Golo Porto']
};

describe('AXE accessibility checks', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('App shell should have no critical accessibility violations', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    await expectNoAxeViolations(fixture.nativeElement);
  });

  it('Login page should have no critical accessibility violations', async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    await expectNoAxeViolations(fixture.nativeElement);
  });

  it('Games list should have no critical accessibility violations', async () => {
    await TestBed.configureTestingModule({
      imports: [GamesListComponent],
      providers: [
        provideRouter([]),
        {
          provide: BackendApiService,
          useValue: {
            getGames: () => of([])
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GamesListComponent);
    fixture.detectChanges();

    await expectNoAxeViolations(fixture.nativeElement);
  });

  it('Game details should have no critical accessibility violations', async () => {
    await TestBed.configureTestingModule({
      imports: [GameDetailsComponent],
      providers: [
        provideRouter([]),
        {
          provide: BackendApiService,
          useValue: {
            getGameById: () => of(mockGameDetails)
          }
        },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ id: mockGameDetails.id }))
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GameDetailsComponent);
    fixture.detectChanges();

    await expectNoAxeViolations(fixture.nativeElement);
  });

  it('Admin area should have no critical accessibility violations', async () => {
    await TestBed.configureTestingModule({
      imports: [AdminGamesComponent],
      providers: [
        {
          provide: BackendApiService,
          useValue: {
            getGames: () => of([])
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AdminGamesComponent);
    fixture.detectChanges();

    await expectNoAxeViolations(fixture.nativeElement);
  });
});

