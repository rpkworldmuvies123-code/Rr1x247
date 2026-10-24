import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ApostasApiService,
  LogAdminEntry,
  UtilizadorAdminResponse
} from '../../core/api/apostas-api.service';

@Component({
  selector: 'app-admin-users',
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink],
  templateUrl: './admin-users.component.html',
  styleUrl: './admin-users.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminUsersComponent {
  private readonly apostasApiService = inject(ApostasApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoading = signal(true);
  protected readonly feedbackMessage = signal('');
  protected readonly feedbackType = signal<'success' | 'error' | ''>('');
  protected readonly utilizadores = signal<ReadonlyArray<UtilizadorAdminResponse>>([]);
  protected readonly selectedUser = signal<UtilizadorAdminResponse | null>(null);
  protected readonly isAdjusting = signal(false);
  protected readonly logEntries = signal<ReadonlyArray<LogAdminEntry>>([]);
  protected readonly isLoadingLog = signal(false);
  protected readonly showLog = signal(false);

  protected readonly hasUtilizadores = computed(() => this.utilizadores().length > 0);

  protected readonly adjustForm = this.formBuilder.nonNullable.group({
    valor: this.formBuilder.nonNullable.control(0, [Validators.required]),
    motivo: this.formBuilder.nonNullable.control('', [Validators.required])
  });

  constructor() {
    this.loadUtilizadores();
  }

  protected selectUser(user: UtilizadorAdminResponse): void {
    this.selectedUser.set(user);
    this.adjustForm.reset({ valor: 0, motivo: '' });
    this.feedbackMessage.set('');
    this.feedbackType.set('');
  }

  protected closePanel(): void {
    this.selectedUser.set(null);
    this.adjustForm.reset({ valor: 0, motivo: '' });
  }

  protected adjustBalance(): void {
    if (this.adjustForm.invalid || this.isAdjusting()) {
      this.adjustForm.markAllAsTouched();
      return;
    }

    const user = this.selectedUser();
    if (!user) return;

    const { valor, motivo } = this.adjustForm.getRawValue();
    if (valor === 0) {
      this.setFeedback('error', 'Amount cannot be zero.');
      return;
    }

    this.isAdjusting.set(true);
    this.feedbackMessage.set('');

    this.apostasApiService
      .ajustarSaldoAdmin(user.Id, valor, motivo)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          const direction = valor > 0 ? 'credited' : 'debited';
          this.setFeedback('success', `${Math.abs(valor).toFixed(2)} EUR ${direction} para ${user.Email}.`);
          this.isAdjusting.set(false);
          this.adjustForm.reset({ valor: 0, motivo: '' });
          this.loadUtilizadores();
        },
        error: (err: any) => {
          this.setFeedback('error', err?.error?.message ?? err?.message ?? 'Failed to adjust balance.');
          this.isAdjusting.set(false);
        }
      });
  }

  protected toggleLog(): void {
    if (!this.showLog()) {
      this.showLog.set(true);
      this.loadLog();
    } else {
      this.showLog.set(false);
    }
  }

  protected refresh(): void {
    this.loadUtilizadores();
  }

  private loadUtilizadores(): void {
    this.isLoading.set(true);

    this.apostasApiService
      .consultarUtilizadoresAdmin()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (utilizadores) => {
          this.utilizadores.set(utilizadores);
          const sel = this.selectedUser();
          if (sel) {
            const refreshed = utilizadores.find((u) => u.Id === sel.Id);
            if (refreshed) this.selectedUser.set(refreshed);
          }
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.utilizadores.set([]);
          this.setFeedback('error', err?.error?.message ?? err?.message ?? 'Failed to load users.');
          this.isLoading.set(false);
        }
      });
  }

  private loadLog(): void {
    this.isLoadingLog.set(true);

    this.apostasApiService
      .consultarLogAdmin()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (entries) => {
          this.logEntries.set(entries);
          this.isLoadingLog.set(false);
        },
        error: () => {
          this.logEntries.set([]);
          this.isLoadingLog.set(false);
        }
      });
  }

  private setFeedback(type: 'success' | 'error', message: string): void {
    this.feedbackType.set(type);
    this.feedbackMessage.set(message);
  }
}
