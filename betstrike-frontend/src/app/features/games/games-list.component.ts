import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { timer } from 'rxjs';
import { BackendApiService } from '../../core/api/backend-api.service';
import {
  GAME_STATUS_LABEL,
  Game,
  GameFilters,
  GameStatus
} from '../../core/models/game.model';

@Component({
  selector: 'app-games-list',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './games-list.component.html',
  styleUrl: './games-list.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GamesListComponent {
  private readonly backendApiService = inject(BackendApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly games = signal<ReadonlyArray<Game>>([]);
  protected readonly hasGames = computed(() => this.games().length > 0);

  protected readonly statusOptions: ReadonlyArray<{ value: ''; label: string } | { value: GameStatus; label: string }> = [
    { value: '', label: 'Todos os estados' },
    { value: 'SCHEDULED', label: GAME_STATUS_LABEL.SCHEDULED },
    { value: 'LIVE', label: GAME_STATUS_LABEL.LIVE },
    { value: 'FINISHED', label: GAME_STATUS_LABEL.FINISHED }
  ];

  protected readonly filterForm = this.formBuilder.nonNullable.group({
    date: '',
    competition: '',
    status: this.formBuilder.nonNullable.control<GameStatus | ''>('')
  });

  constructor() {
    timer(0, 10_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.fetchGames(this.currentFilters());
      });
  }

  protected applyFilters(): void {
    this.fetchGames(this.currentFilters());
  }

  protected clearFilters(): void {
    this.filterForm.reset({
      date: '',
      competition: '',
      status: ''
    });

    this.fetchGames({});
  }

  protected statusLabel(status: GameStatus): string {
    return GAME_STATUS_LABEL[status];
  }

  private fetchGames(filters: GameFilters): void {
    this.errorMessage.set('');
    this.isLoading.set(true);

    this.backendApiService
      .getGames(filters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (games) => {
          this.games.set(games);
          this.isLoading.set(false);
        },
        error: () => {
          this.errorMessage.set('Nao foi possivel carregar os jogos.');
          this.isLoading.set(false);
        }
      });
  }

  private currentFilters(): GameFilters {
    const raw = this.filterForm.getRawValue();

    return {
      date: raw.date || undefined,
      competition: raw.competition.trim() || undefined,
      status: raw.status || undefined
    };
  }
}

