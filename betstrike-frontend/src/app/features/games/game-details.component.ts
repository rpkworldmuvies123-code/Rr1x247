import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { BackendApiService } from '../../core/api/backend-api.service';
import { GAME_STATUS_LABEL, GameDetails } from '../../core/models/game.model';

@Component({
  selector: 'app-game-details',
  imports: [RouterLink, DatePipe],
  templateUrl: './game-details.component.html',
  styleUrl: './game-details.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly backendApiService = inject(BackendApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly game = signal<GameDetails | null>(null);
  protected readonly statusLabel = computed(() => {
    const game = this.game();
    return game ? GAME_STATUS_LABEL[game.status] : '';
  });

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const gameId = params.get('id') ?? '';
          this.errorMessage.set('');
          this.isLoading.set(true);
          return this.backendApiService.getGameById(gameId);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (game) => {
          this.game.set(game);
          this.isLoading.set(false);
        },
        error: () => {
          this.errorMessage.set('Nao foi possivel carregar o detalhe do jogo.');
          this.isLoading.set(false);
        }
      });
  }
}

