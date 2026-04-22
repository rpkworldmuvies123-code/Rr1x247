import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, catchError, forkJoin, interval, of, switchMap } from 'rxjs';
import { ApostasApiService, EstatisticasJogoResponse, ResultadoResponse } from '../../core/api/apostas-api.service';
import { JogosService } from '../../core/api/jogos.service';
import { GAME_STATE_LABEL, GameDetails } from '../../core/models/game.model';

@Component({
  selector: 'app-game-details',
  imports: [RouterLink, DatePipe],
  templateUrl: './game-details.component.html',
  styleUrl: './game-details.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly jogosService = inject(JogosService);
  private readonly apostasApiService = inject(ApostasApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly game = signal<GameDetails | null>(null);
  protected readonly resultado = signal<ResultadoResponse | null>(null);
  protected readonly estatisticas = signal<EstatisticasJogoResponse | null>(null);
  protected readonly statusLabel = computed(() => {
    const game = this.game();
    if (!game) return '';
    return (GAME_STATE_LABEL as any)[game.estado ?? 1] || 'Desconhecido';
  });

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const codigoJogo = params.get('id') ?? '';
          this.errorMessage.set('');
          this.isLoading.set(true);

          if (!codigoJogo) {
            this.errorMessage.set('Codigo do jogo nao encontrado.');
            this.isLoading.set(false);
            return of(null);
          }

          return this.loadGameData(codigoJogo);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (data) => {
          if (!data) {
            return;
          }

          this.game.set(this.mapJogoDetalheToGameDetails(data.jogo));
          this.resultado.set(data.resultado);
          this.estatisticas.set(data.estatisticas);
          this.isLoading.set(false);
        },
        error: (error: any) => {
          this.errorMessage.set('Não foi possível carregar o detalhe do jogo. ' + error.message);
          this.isLoading.set(false);
        }
      });

    // Atualizar a cada 10 segundos
    interval(10_000)
      .pipe(
        switchMap(() => {
          const codigoJogo = this.route.snapshot.paramMap.get('id') ?? '';
          if (!codigoJogo) {
            return of(null);
          }

          return this.loadGameData(codigoJogo);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (data) => {
          if (!data) {
            return;
          }

          this.game.set(this.mapJogoDetalheToGameDetails(data.jogo));
          this.resultado.set(data.resultado);
          this.estatisticas.set(data.estatisticas);
        }
      });
  }

  private loadGameData(codigoJogo: string): Observable<{
    jogo: any;
    resultado: ResultadoResponse | null;
    estatisticas: EstatisticasJogoResponse | null;
  }> {
    return forkJoin({
      jogo: this.jogosService.obterPorCodigo(codigoJogo),
      resultado: this.apostasApiService.consultarResultado(codigoJogo).pipe(catchError(() => of(null))),
      estatisticas: this.apostasApiService.consultarEstatisticasJogo(codigoJogo).pipe(catchError(() => of(null)))
    });
  }

  private mapJogoDetalheToGameDetails(jogo: any): GameDetails {
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
      venue: undefined,
      events: [],
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
}

