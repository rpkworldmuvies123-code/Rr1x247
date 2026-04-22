import { DatePipe, CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { timer } from 'rxjs';
import { JogosService, JogoListaItemResponse } from '../../core/api/jogos.service';
import {
  GAME_STATUS_LABEL,
  GAME_STATE_LABEL,
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
  private readonly jogosService = inject(JogosService);
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

  protected readonly statusOptions: ReadonlyArray<{ value: string; label: string }> = [
    { value: '', label: 'Todos os estados' },
    { value: '1', label: 'Agendado' },
    { value: '2', label: 'Em Curso' },
    { value: '3', label: 'Finalizado' },
    { value: '4', label: 'Cancelado' },
    { value: '5', label: 'Adiado' }
  ];

  protected readonly filterForm = this.formBuilder.nonNullable.group({
    date: '',
    estado: ''
  });

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.clearGoalNotificationTimers();
    });

    // Atualizar a cada 10 segundos
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
      estado: ''
    });

    this.fetchGames({});
  }

  protected statusLabel(estado: number | string): string {
    const numEstado = typeof estado === 'string' ? parseInt(estado, 10) : estado;
    return (GAME_STATE_LABEL as any)[numEstado] || 'Desconhecido';
  }

  private fetchGames(filters: { date?: string; estado?: string }): void {
    this.errorMessage.set('');
    this.isLoading.set(true);

    const filtros: any = {};

    if (filters.date) {
      filtros.dataInicio = filters.date;
      filtros.dataFim = filters.date;
    }

    if (filters.estado) {
      filtros.estado = parseInt(filters.estado, 10);
    }

    this.jogosService
      .listar(filtros)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (jogos) => {
          const games = jogos.map((jogo) => this.mapJogoToGame(jogo));
          this.detectGoals(games);
          this.updatePreviousScores(games);
          this.games.set(games);
          this.isLoading.set(false);
        },
        error: (error: any) => {
          this.errorMessage.set('Não foi possível carregar os jogos. ' + error.message);
          this.isLoading.set(false);
        }
      });
  }

  private currentFilters(): { date?: string; estado?: string } {
    const raw = this.filterForm.getRawValue();

    return {
      date: raw.date?.trim() || undefined,
      estado: raw.estado?.trim() || undefined
    };
  }

  private mapJogoToGame(jogo: JogoListaItemResponse): Game {
    return {
      id: jogo.CodigoJogo,
      code: jogo.CodigoJogo,
      homeTeam: jogo.EquipaCasa,
      awayTeam: jogo.EquipaFora,
      competition: jogo.TipoCompeticao,
      startTime: new Date(jogo.DataHoraInicio).toISOString(),
      status: this.mapEstadoToGameStatus(jogo.Estado),
      homeScore: jogo.GolosCasa,
      awayScore: jogo.GolosVisitante,
      estado: jogo.Estado
    };
  }

  private mapEstadoToGameStatus(estado: number): 'SCHEDULED' | 'LIVE' | 'FINISHED' {
    switch (estado) {
      case 1:
        return 'SCHEDULED';
      case 2:
        return 'LIVE';
      case 3:
        return 'FINISHED';
      case 4:
      case 5:
        return 'SCHEDULED';
      default:
        return 'SCHEDULED';
    }
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

