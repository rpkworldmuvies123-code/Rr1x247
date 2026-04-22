import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { timer } from 'rxjs';
import { BackendApiService } from '../../core/api/backend-api.service';
import { Game, estadoClass, estadoLabel } from '../../core/models/game.model';

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
  protected readonly estadoLabel = estadoLabel;
  protected readonly estadoClass = estadoClass;

  protected readonly statusOptions: ReadonlyArray<{ value: string; label: string }> = [
    { value: 'open', label: 'Apostas abertas' },
    { value: '', label: 'Todos os estados' },
    { value: '1', label: 'Agendado' },
    { value: '2', label: 'Em Curso' },
    { value: '3', label: 'Finalizado' },
    { value: '4', label: 'Cancelado' },
    { value: '5', label: 'Adiado' }
  ];

  protected readonly filterForm = this.formBuilder.nonNullable.group({
    date: '',
    estado: 'open'
  });

  constructor() {
    this.destroyRef.onDestroy(() => this.clearGoalNotificationTimers());

    timer(0, 10_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.fetchGames());
  }

  protected applyFilters(): void {
    this.fetchGames();
  }

  protected clearFilters(): void {
    this.filterForm.reset({ date: '', estado: 'open' });
    this.fetchGames();
  }

  protected canBet(game: Game): boolean {
    return game.estado === 1 || game.estado === 2;
  }

  private fetchGames(): void {
    this.errorMessage.set('');
    this.isLoading.set(true);

    const raw = this.filterForm.getRawValue();
    const isOpenFilter = raw.estado?.trim() === 'open';
    const filters: { date?: string; estado?: number } = {};
    if (raw.date?.trim()) filters.date = raw.date.trim();
    if (!isOpenFilter && raw.estado?.trim()) filters.estado = parseInt(raw.estado.trim(), 10);

    this.backendApiService
      .getGames(filters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (games) => {
          const list = isOpenFilter
            ? [...games].filter((g) => g.estado === 1 || g.estado === 2)
            : [...games];
          this.detectGoals(list);
          this.updatePreviousScores(list);
          this.games.set(list);
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.errorMessage.set(err?.error?.message ?? err?.message ?? 'Não foi possível carregar os jogos.');
          this.isLoading.set(false);
        }
      });
  }

  private detectGoals(games: ReadonlyArray<Game>): void {
    for (const game of games) {
      const prev = this.previousScoresByGame.get(game.id);
      if (!prev) continue;

      let h = prev.homeScore;
      let a = prev.awayScore;

      while (h < game.homeScore) {
        h++;
        this.addGoalNotification(`Golo! ${game.homeTeam} ${h} – ${a} ${game.awayTeam}`);
      }
      while (a < game.awayScore) {
        a++;
        this.addGoalNotification(`Golo! ${game.homeTeam} ${h} – ${a} ${game.awayTeam}`);
      }
    }
  }

  private addGoalNotification(message: string): void {
    const id = `goal-${++this.goalNotificationSequence}`;
    let removed: ReadonlyArray<GoalNotification> = [];

    this.goalNotifications.update((current) => {
      const next = [{ id, message }, ...current];
      if (next.length <= 4) return next;
      removed = next.slice(4);
      return next.slice(0, 4);
    });

    for (const n of removed) this.clearGoalNotificationTimer(n.id);

    const tid = setTimeout(() => {
      this.goalNotifications.update((c) => c.filter((n) => n.id !== id));
      this.goalNotificationTimers.delete(id);
    }, this.notificationDurationMs);

    this.goalNotificationTimers.set(id, tid);
  }

  private updatePreviousScores(games: ReadonlyArray<Game>): void {
    this.previousScoresByGame.clear();
    for (const g of games) {
      this.previousScoresByGame.set(g.id, { homeScore: g.homeScore, awayScore: g.awayScore });
    }
  }

  private clearGoalNotificationTimer(id: string): void {
    const tid = this.goalNotificationTimers.get(id);
    if (tid) { clearTimeout(tid); this.goalNotificationTimers.delete(id); }
  }

  private clearGoalNotificationTimers(): void {
    for (const tid of this.goalNotificationTimers.values()) clearTimeout(tid);
    this.goalNotificationTimers.clear();
  }
}
