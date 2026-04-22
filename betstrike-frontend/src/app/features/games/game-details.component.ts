import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, interval } from 'rxjs';
import { JogosService } from '../../core/api/jogos.service';
import { GAME_STATUS_LABEL, GAME_STATE_LABEL, GameDetails } from '../../core/models/game.model';

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
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly game = signal<GameDetails | null>(null);
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
          return this.jogosService.obterPorCodigo(codigoJogo);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (jogo) => {
          this.game.set(this.mapJogoDetalheToGameDetails(jogo));
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
            return new Promise((reject) => reject('Código do jogo não encontrado'));
          }
          return this.jogosService.obterPorCodigo(codigoJogo);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (jogo) => {
          this.game.set(this.mapJogoDetalheToGameDetails(jogo));
        }
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

