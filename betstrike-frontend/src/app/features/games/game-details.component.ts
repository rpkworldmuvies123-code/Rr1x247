import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, catchError, forkJoin, interval, of, switchMap } from 'rxjs';
import { ApostasApiService, EstatisticasJogoResponse, ResultadoResponse } from '../../core/api/apostas-api.service';
import { BackendApiService } from '../../core/api/backend-api.service';
import { GameDetails, estadoClass, estadoLabel } from '../../core/models/game.model';

@Component({
  selector: 'app-game-details',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './game-details.component.html',
  styleUrl: './game-details.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly backendApiService = inject(BackendApiService);
  private readonly apostasApiService = inject(ApostasApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly game = signal<GameDetails | null>(null);
  protected readonly resultado = signal<ResultadoResponse | null>(null);
  protected readonly estatisticas = signal<EstatisticasJogoResponse | null>(null);
  protected readonly statusLabel = computed(() => estadoLabel(this.game()?.estado ?? 0));
  protected readonly statusClass = computed(() => estadoClass(this.game()?.estado ?? 0));

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const codigoJogo = params.get('id') ?? '';
          this.errorMessage.set('');
          this.isLoading.set(true);

          if (!codigoJogo) {
            this.errorMessage.set('Código do jogo não encontrado.');
            this.isLoading.set(false);
            return of(null);
          }

          return this.loadGameData(codigoJogo);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (data) => {
          if (!data) return;
          this.game.set(data.jogo);
          this.resultado.set(data.resultado);
          this.estatisticas.set(data.estatisticas);
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.errorMessage.set(err?.error?.message ?? err?.message ?? 'Não foi possível carregar o jogo.');
          this.isLoading.set(false);
        }
      });

    interval(10_000)
      .pipe(
        switchMap(() => {
          const codigoJogo = this.route.snapshot.paramMap.get('id') ?? '';
          return codigoJogo ? this.loadGameData(codigoJogo) : of(null);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (data) => {
          if (!data) return;
          this.game.set(data.jogo);
          this.resultado.set(data.resultado);
          this.estatisticas.set(data.estatisticas);
        }
      });
  }

  private loadGameData(codigoJogo: string): Observable<{
    jogo: GameDetails;
    resultado: ResultadoResponse | null;
    estatisticas: EstatisticasJogoResponse | null;
  }> {
    return forkJoin({
      jogo: this.backendApiService.getGameById(codigoJogo),
      resultado: this.apostasApiService.consultarResultado(codigoJogo).pipe(catchError(() => of(null))),
      estatisticas: this.apostasApiService.consultarEstatisticasJogo(codigoJogo).pipe(catchError(() => of(null)))
    });
  }
}
