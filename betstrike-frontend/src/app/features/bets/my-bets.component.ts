import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApostaResponse, ApostasApiService } from '../../core/api/apostas-api.service';

interface BetViewModel {
  id: number;
  codigoJogo: string;
  equipaCasa: string;
  equipaFora: string;
  tipoAposta: string;
  montanteApostado: number;
  oddMomento: number;
  premioPotencial: number;
  estado: number;
  dataHoraRegisto: string;
}

@Component({
  selector: 'app-my-bets',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './my-bets.component.html',
  styleUrl: './my-bets.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MyBetsComponent {
  private readonly apostasApiService = inject(ApostasApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal('');
  protected readonly feedbackMessage = signal('');
  protected readonly cancelingBetId = signal<number | null>(null);
  protected readonly bets = signal<ReadonlyArray<BetViewModel>>([]);
  protected readonly hasBets = computed(() => this.bets().length > 0);

  constructor() {
    this.loadBets();
  }

  protected estadoLabel(estado: number): string {
    switch (estado) {
      case 1: return 'Pending';
      case 2: return 'Won';
      case 3: return 'Lost';
      case 4: return 'Voided';
      default: return 'Unknown';
    }
  }

  protected betEstadoClass(estado: number): string {
    switch (estado) {
      case 1: return 'agendado';
      case 2: return 'finalizado';
      case 3: return 'cancelado';
      case 4: return 'adiado';
      default: return 'outro';
    }
  }

  protected canCancel(aposta: BetViewModel): boolean {
    return aposta.estado === 1;
  }

  protected refresh(): void {
    this.loadBets();
  }

  protected cancelBet(aposta: BetViewModel): void {
    if (!this.canCancel(aposta) || this.cancelingBetId() !== null) {
      return;
    }

    this.errorMessage.set('');
    this.feedbackMessage.set('');
    this.cancelingBetId.set(aposta.id);

    this.apostasApiService
      .cancelarAposta(aposta.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.feedbackMessage.set(`Aposta #${aposta.id} cancelled successfully.`);
          this.cancelingBetId.set(null);
          this.loadBets();
        },
        error: (err: any) => {
          this.errorMessage.set(err?.error?.message ?? err?.message ?? 'Unable to cancel bet.');
          this.cancelingBetId.set(null);
        }
      });
  }

  private loadBets(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.apostasApiService
      .listarApostas()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (apostas) => {
          this.bets.set(apostas.map((aposta) => this.toViewModel(aposta)));
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.bets.set([]);
          this.errorMessage.set(err?.error?.message ?? err?.message ?? 'Unable to load bets.');
          this.isLoading.set(false);
        }
      });
  }

  private toViewModel(aposta: ApostaResponse): BetViewModel {
    return {
      id: aposta.Id,
      codigoJogo: aposta.CodigoJogo,
      equipaCasa: aposta.EquipaCasa,
      equipaFora: aposta.EquipaFora,
      tipoAposta: aposta.TipoAposta,
      montanteApostado: aposta.MontanteApostado,
      oddMomento: aposta.OddMomento,
      premioPotencial: aposta.PremioPotencial,
      estado: aposta.Estado,
      dataHoraRegisto: aposta.DataHoraRegisto
    };
  }
}

