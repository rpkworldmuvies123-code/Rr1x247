import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { BackendApiService } from '../../core/api/backend-api.service';
import {
  BET_MARKET_LABEL,
  DEFAULT_BETTING_GAME,
  GAME_STATE_LABEL,
  BettingGame,
  BetMarket,
  GameDetails,
  GameStatus,
  MatchState
} from '../../core/models/game.model';

@Component({
  selector: 'app-game-bet',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './game-bet.component.html',
  styleUrl: './game-bet.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameBetComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly backendApiService = inject(BackendApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly gameId = signal('default');
  protected readonly isLoading = signal(true);
  protected readonly isDefaultGame = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly confirmationMessage = signal('');
  protected readonly game = signal<BettingGame>(DEFAULT_BETTING_GAME);
  protected readonly betOptions: ReadonlyArray<BetMarket> = ['1', 'X', '2'];
  protected readonly betMarketLabels = BET_MARKET_LABEL;
  protected readonly gameStateLabels = GAME_STATE_LABEL;
  protected readonly stateLabel = computed(() => this.gameStateLabels[this.game().estadoDoJogo]);
  protected readonly sourceLabel = computed(() => (this.isDefaultGame() ? 'Jogo predefinido' : 'Jogo carregado da API'));

  protected readonly betForm = this.formBuilder.nonNullable.group({
    outcome: this.formBuilder.nonNullable.control<BetMarket>('1', [Validators.required])
  });

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const gameId = params.get('id');
          this.gameId.set(gameId ?? 'default');
          this.isLoading.set(true);
          this.errorMessage.set('');
          this.confirmationMessage.set('');
          this.isDefaultGame.set(true);

          if (!gameId) {
            this.isLoading.set(false);
            this.errorMessage.set('Nao foi possivel identificar o jogo selecionado. A mostrar o jogo default.');
            return of(null);
          }

          return this.backendApiService.getGameById(gameId);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (game) => {
          if (game) {
            this.game.set(this.toBettingGame(game));
            this.isDefaultGame.set(false);
          }

          this.isLoading.set(false);
        },
        error: () => {
          this.errorMessage.set('Nao foi possivel carregar o jogo. A mostrar o jogo default.');
          this.isLoading.set(false);
        }
      });
  }

  protected placeBet(): void {
    if (this.betForm.invalid) {
      this.betForm.markAllAsTouched();
      return;
    }

    const { outcome } = this.betForm.getRawValue();
    const selectedGame = this.game();

    this.confirmationMessage.set(
      `Aposta registada em demonstracao: ${outcome} - ${this.betMarketLabels[outcome]} em ${selectedGame.equipaCasa} vs ${selectedGame.equipaFora}.`
    );
  }

  private toBettingGame(game: GameDetails): BettingGame {
    const [datePart = '', timePart = ''] = game.startTime.split('T');

    return {
      dataDoJogo: this.formatDate(datePart),
      horaDeInicio: this.formatTime(timePart),
      equipaCasa: game.homeTeam,
      equipaFora: game.awayTeam,
      golosCasa: game.homeScore,
      golosFora: game.awayScore,
      estadoDoJogo: this.resolveMatchState(game.status)
    };
  }

  private formatDate(datePart: string): string {
    const [year, month, day] = datePart.split('-');

    if (!year || !month || !day) {
      return DEFAULT_BETTING_GAME.dataDoJogo;
    }

    return `${day}/${month}/${year}`;
  }

  private formatTime(timePart: string): string {
    if (!timePart) {
      return DEFAULT_BETTING_GAME.horaDeInicio;
    }

    return timePart.slice(0, 5) || DEFAULT_BETTING_GAME.horaDeInicio;
  }

  private resolveMatchState(status: GameStatus): MatchState {
    switch (status) {
      case 'LIVE':
        return 2;
      case 'FINISHED':
        return 3;
      default:
        return 1;
    }
  }
}

