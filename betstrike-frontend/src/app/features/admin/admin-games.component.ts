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
  protected readonly statusOptions: ReadonlyArray<GameStatus> = ['SCHEDULED', 'LIVE', 'FINISHED'];

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
          this.setFeedback('success', 'Jogo criado com sucesso.');
          this.createForm.reset({
            code: '',
            homeTeam: '',
            awayTeam: '',
            competition: '',
            startTime: ''
          });
          this.loadGames();
        },
        error: () => {
          this.setFeedback('error', 'Falha ao criar jogo. Verifica dados e codigo unico.');
        }
      });
  }

  protected updateGame(): void {
    const game = this.editingGame();

    if (!game) {
      this.setFeedback('error', 'Seleciona um jogo para editar.');
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
          this.setFeedback('success', 'Jogo atualizado com sucesso.');
          this.closeEditDialog();
          this.loadGames();
        },
        error: () => {
          this.setFeedback('error', 'Falha ao atualizar o jogo.');
        }
      });
  }

  protected deleteGame(game: Game): void {
    if (!this.canDelete(game)) {
      this.setFeedback('error', 'Apenas jogos agendados podem ser removidos.');
      return;
    }

    this.backendApiService
      .deleteGame(game.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.setFeedback('success', 'Jogo removido com sucesso.');
          this.loadGames();
        },
        error: () => {
          this.setFeedback('error', 'Falha ao remover jogo.');
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
        error: () => {
          this.games.set([]);
          this.isLoading.set(false);
          this.setFeedback('error', 'Nao foi possivel carregar os jogos.');
        }
      });
  }

  private setFeedback(type: 'success' | 'error', message: string): void {
    this.feedbackType.set(type);
    this.feedbackMessage.set(message);
  }
}
