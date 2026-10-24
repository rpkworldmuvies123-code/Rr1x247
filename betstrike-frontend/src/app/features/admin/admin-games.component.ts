import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BackendApiService } from '../../core/api/backend-api.service';
import {
  CreateGameRequest,
  GAME_STATUS_LABEL,
  Game,
  GameStatus,
  UpdateGameRequest
} from '../../core/models/game.model';

@Component({
  selector: 'app-admin-games',
  imports: [ReactiveFormsModule],
  templateUrl: './admin-games.component.html',
  styleUrl: './admin-games.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:keydown.escape)': 'closeEditDialog()'
  }
})
export class AdminGamesComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly backendApiService = inject(BackendApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly feedbackMessage = signal('');
  protected readonly feedbackType = signal<'success' | 'error' | ''>('');
  protected readonly games = signal<ReadonlyArray<Game>>([]);
  protected readonly editingGame = signal<Game | null>(null);

  protected readonly hasGames = computed(() => this.games().length > 0);
  protected readonly isEditOpen = computed(() => this.editingGame() !== null);
  protected readonly statusOptions: ReadonlyArray<GameStatus> = ['SCHEDULED', 'LIVE', 'FINISHED', 'CANCELLED', 'POSTPONED'];

  protected readonly createForm = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required]],
    homeTeam: ['', [Validators.required]],
    awayTeam: ['', [Validators.required]],
    competition: ['', [Validators.required]],
    startTime: ['', [Validators.required]]
  });

  protected readonly updateForm = this.formBuilder.nonNullable.group({
    status: this.formBuilder.nonNullable.control<GameStatus>('SCHEDULED', [Validators.required]),
    homeScore: this.formBuilder.nonNullable.control(0, [Validators.min(0)]),
    awayScore: this.formBuilder.nonNullable.control(0, [Validators.min(0)])
  });

  constructor() {
    this.loadGames();
  }

  protected statusLabel(status: GameStatus): string {
    return GAME_STATUS_LABEL[status];
  }

  protected canDelete(game: Game): boolean {
    return game.status === 'SCHEDULED';
  }

  protected openEditDialog(game: Game): void {
    this.editingGame.set(game);
    this.updateForm.reset({
      status: game.status,
      homeScore: game.homeScore,
      awayScore: game.awayScore
    });
  }

  protected closeEditDialog(): void {
    this.editingGame.set(null);
    this.updateForm.reset({
      status: 'SCHEDULED',
      homeScore: 0,
      awayScore: 0
    });
  }

  protected createGame(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const payload = this.createForm.getRawValue() as CreateGameRequest;

    this.backendApiService
      .createGame(payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.setFeedback('success', 'Match created successfully.');
          this.createForm.reset({
            code: '',
            homeTeam: '',
            awayTeam: '',
            competition: '',
            startTime: ''
          });
          this.loadGames();
        },
        error: (err: any) => {
          this.setFeedback('error', err?.error?.message ?? err?.message ?? 'Failed to create match.');
        }
      });
  }

  protected updateGame(): void {
    const game = this.editingGame();

    if (!game) {
      this.setFeedback('error', 'Select a match to edit.');
      return;
    }

    if (this.updateForm.invalid) {
      this.updateForm.markAllAsTouched();
      return;
    }

    const raw = this.updateForm.getRawValue();
    const payload: UpdateGameRequest = {
      status: raw.status,
      homeScore: raw.homeScore,
      awayScore: raw.awayScore
    };

    this.backendApiService
      .updateGame(game.id, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.setFeedback('success', 'Match updated successfully.');
          this.closeEditDialog();
          this.loadGames();
        },
        error: (err: any) => {
          this.setFeedback('error', err?.error?.message ?? err?.message ?? 'Failed to update match.');
        }
      });
  }

  protected deleteGame(game: Game): void {
    if (!this.canDelete(game)) {
      this.setFeedback('error', 'Only scheduled matches can be removed.');
      return;
    }

    this.backendApiService
      .deleteGame(game.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.setFeedback('success', 'Match removed successfully.');
          this.loadGames();
        },
        error: (err: any) => {
          this.setFeedback('error', err?.error?.message ?? err?.message ?? 'Failed to remove match.');
        }
      });
  }

  private loadGames(): void {
    this.isLoading.set(true);

    this.backendApiService
      .getGames()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (games) => {
          this.games.set(games);
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.games.set([]);
          this.isLoading.set(false);
          this.setFeedback('error', err?.error?.message ?? err?.message ?? 'Unable to load matches.');
        }
      });
  }

  private setFeedback(type: 'success' | 'error', message: string): void {
    this.feedbackType.set(type);
    this.feedbackMessage.set(message);
  }
}
