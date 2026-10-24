import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';
import { ApostasApiService } from '../../core/api/apostas-api.service';
import { BackendApiService } from '../../core/api/backend-api.service';
import { BET_MARKET_LABEL, BetMarket, GameDetails, estadoClass, estadoLabel } from '../../core/models/game.model';

@Component({
  selector: 'app-game-bet',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './game-bet.component.html',
  styleUrl: './game-bet.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GameBetComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly backendApiService = inject(BackendApiService);
  private readonly apostasApiService = inject(ApostasApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly codigoJogo = signal<string | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isOddsLoading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly confirmationMessage = signal('');
  protected readonly isSubmittingBet = signal(false);
  protected readonly walletBalance = signal<number | null>(null);
  protected readonly game = signal<GameDetails | null>(null);

  protected readonly betOptions: ReadonlyArray<BetMarket> = ['1', 'X', '2'];
  protected readonly betMarketLabels = BET_MARKET_LABEL;

  protected readonly stateLabel = computed(() => estadoLabel(this.game()?.estado ?? 0));
  protected readonly stateClass = computed(() => estadoClass(this.game()?.estado ?? 0));
  protected readonly canBet = computed(() => {
    const estado = this.game()?.estado;
    return estado === 1 || estado === 2;
  });

  protected readonly betForm = this.formBuilder.nonNullable.group({
    outcome: this.formBuilder.nonNullable.control<BetMarket>('1', [Validators.required]),
    amount: this.formBuilder.nonNullable.control(5, [Validators.required, Validators.min(0.01)])
  });

  private readonly _formSnapshot = signal(this.betForm.getRawValue());
  protected readonly prizePreview = computed(() => {
    const snap = this._formSnapshot();
    const odd = this.oddForOutcome(snap.outcome);
    if (!odd || snap.amount <= 0) return null;
    return odd * snap.amount;
  });

  constructor() {
    this.loadBalance();

    this.betForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this._formSnapshot.set(this.betForm.getRawValue()));

    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const id = params.get('id');
          this.codigoJogo.set(id);
          this.isLoading.set(true);
          this.errorMessage.set('');
          this.confirmationMessage.set('');
          this.game.set(null);

          if (!id) {
            this.isLoading.set(false);
            this.errorMessage.set('Match code not found.');
            return of(null);
          }

          return this.backendApiService.getGameById(id);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (result) => {
          if (!result) return;
          this.game.set(result);
          this.isLoading.set(false);
          if (!result.odds) {
            this.fetchOddsFallback(result.id);
          }
        },
        error: (err: any) => {
          this.errorMessage.set(err?.error?.message ?? err?.message ?? 'Unable to load match.');
          this.isLoading.set(false);
        }
      });
  }

  protected placeBet(): void {
    if (this.betForm.invalid || this.isSubmittingBet()) {
      this.betForm.markAllAsTouched();
      return;
    }

    if (!this.canBet()) {
      this.errorMessage.set('This match is not accepting bets in its current state.');
      return;
    }

    const codigoJogo = this.codigoJogo();
    if (!codigoJogo) {
      this.errorMessage.set('Invalid match code.');
      return;
    }

    const { outcome, amount } = this.betForm.getRawValue();
    const game = this.game();

    this.isSubmittingBet.set(true);
    this.errorMessage.set('');
    this.confirmationMessage.set('');

    this.apostasApiService
      .criarAposta({ codigoJogo, tipoAposta: outcome, montanteApostado: amount })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const odd = this.oddForOutcome(outcome);
          const prize = odd ? (amount * odd).toFixed(2) : '—';
          this.confirmationMessage.set(
            `Aposta #${response.id} placed — ${this.betMarketLabels[outcome]} (odd ${odd?.toFixed(2) ?? '—'}) · potential payout ${prize} EUR.`
          );
          this.loadBalance();
          this.isSubmittingBet.set(false);
        },
        error: (err: any) => {
          this.errorMessage.set(err?.error?.message ?? err?.message ?? 'Unable to place bet.');
          this.isSubmittingBet.set(false);
        }
      });
  }

  protected oddForOutcome(outcome: BetMarket): number | null {
    const o = this.game()?.odds;
    if (!o) return null;
    switch (outcome) {
      case '1': return o.Odd1;
      case 'X': return o.OddX;
      case '2': return o.Odd2;
    }
  }

  private fetchOddsFallback(codigoJogo: string): void {
    this.isOddsLoading.set(true);
    this.backendApiService
      .getGameOdds(codigoJogo)
      .pipe(takeUntilDestroyed(this.destroyRef), catchError(() => of(null)))
      .subscribe((odds) => {
        if (odds) {
          this.game.update((g) => (g ? { ...g, odds } : g));
        }
        this.isOddsLoading.set(false);
      });
  }

  private loadBalance(): void {
    this.apostasApiService
      .consultarSaldo()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (saldo) => this.walletBalance.set(saldo.SaldoAtual),
        error: () => this.walletBalance.set(null)
      });
  }
}
