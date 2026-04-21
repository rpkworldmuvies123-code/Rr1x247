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

interface GoalNotification {
  id: string;
  message: string;
}

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
  private readonly notificationDurationMs = 10_000;
  private readonly goalNotificationTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private readonly previousScoresByGame = new Map<string, { homeScore: number; awayScore: number }>();
  private goalNotificationSequence = 0;

  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly games = signal<ReadonlyArray<Game>>([]);
  protected readonly hasGames = computed(() => this.games().length > 0);
  protected readonly goalNotifications = signal<ReadonlyArray<GoalNotification>>([]);

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
    this.destroyRef.onDestroy(() => {
      this.clearGoalNotificationTimers();
    });

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
          this.detectGoals(games);
          this.updatePreviousScores(games);
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

  private detectGoals(games: ReadonlyArray<Game>): void {
    for (const game of games) {
      const previousScore = this.previousScoresByGame.get(game.id);

      if (!previousScore) {
        continue;
      }

      let trackedHomeScore = previousScore.homeScore;
      let trackedAwayScore = previousScore.awayScore;

      while (trackedHomeScore < game.homeScore) {
        trackedHomeScore += 1;
        this.addGoalNotification(
          `Golo do ${game.homeTeam}: ${game.homeTeam} ${trackedHomeScore} - ${trackedAwayScore} ${game.awayTeam}`
        );
      }

      while (trackedAwayScore < game.awayScore) {
        trackedAwayScore += 1;
        this.addGoalNotification(
          `Golo do ${game.awayTeam}: ${game.homeTeam} ${trackedHomeScore} - ${trackedAwayScore} ${game.awayTeam}`
        );
      }
    }
  }

  private addGoalNotification(message: string): void {
    const notificationId = `goal-${++this.goalNotificationSequence}`;

    let removedNotifications: ReadonlyArray<GoalNotification> = [];
    this.goalNotifications.update((current) => {
      const next = [{ id: notificationId, message }, ...current];

      if (next.length <= 4) {
        return next;
      }

      removedNotifications = next.slice(4);
      return next.slice(0, 4);
    });

    for (const removedNotification of removedNotifications) {
      this.clearGoalNotificationTimer(removedNotification.id);
    }

    const timeoutId = setTimeout(() => {
      this.goalNotifications.update((current) => current.filter((notification) => notification.id !== notificationId));
      this.goalNotificationTimers.delete(notificationId);
    }, this.notificationDurationMs);

    this.goalNotificationTimers.set(notificationId, timeoutId);
  }

  private updatePreviousScores(games: ReadonlyArray<Game>): void {
    this.previousScoresByGame.clear();

    for (const game of games) {
      this.previousScoresByGame.set(game.id, {
        homeScore: game.homeScore,
        awayScore: game.awayScore
      });
    }
  }

  private clearGoalNotificationTimer(notificationId: string): void {
    const timeoutId = this.goalNotificationTimers.get(notificationId);

    if (!timeoutId) {
      return;
    }

    clearTimeout(timeoutId);
    this.goalNotificationTimers.delete(notificationId);
  }

  private clearGoalNotificationTimers(): void {
    for (const timeoutId of this.goalNotificationTimers.values()) {
      clearTimeout(timeoutId);
    }

    this.goalNotificationTimers.clear();
  }
}

