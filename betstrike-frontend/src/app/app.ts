import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApostasApiService } from './core/api/apostas-api.service';
import { AuthService } from './core/auth/auth.service';

type ThemeMode = 'dark' | 'light';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly authService = inject(AuthService);
  private readonly apostasApiService = inject(ApostasApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly themeStorageKey = 'betstrike.theme';
  private readonly balanceRefreshMs = 15_000;

  protected readonly isAuthenticated = computed(() => this.authService.isAuthenticated());
  protected readonly isAdmin = computed(() => this.authService.isAdmin());
  protected readonly username = computed(() => this.authService.currentSession()?.nomeCompleto ?? this.authService.currentSession()?.email ?? '');
  protected readonly isAdminMode = computed(() => this.isAuthenticated() && this.isAdmin());
  protected readonly themeMode = signal<ThemeMode>(this.readInitialTheme());
  protected readonly isLightMode = computed(() => this.themeMode() === 'light');
  protected readonly balance = signal<number | null>(null);
  protected readonly isBalanceLoading = signal(false);
  protected readonly balanceLabel = computed(() => {
    const currentBalance = this.balance();
    if (currentBalance === null) {
      return '--';
    }

    return `${currentBalance.toFixed(2)} EUR`;
  });

  constructor() {
    this.applyTheme(this.themeMode());

    effect(() => {
      if (!this.isAuthenticated()) {
        this.balance.set(null);
        this.isBalanceLoading.set(false);
        return;
      }

      this.loadBalance();
    }, { allowSignalWrites: true });

    const intervalId = window.setInterval(() => {
      if (this.isAuthenticated()) {
        this.loadBalance();
      }
    }, this.balanceRefreshMs);

    this.destroyRef.onDestroy(() => {
      window.clearInterval(intervalId);
    });
  }


  protected toggleTheme(): void {
    const nextTheme: ThemeMode = this.themeMode() === 'dark' ? 'light' : 'dark';
    this.themeMode.set(nextTheme);
    this.applyTheme(nextTheme);
  }

  protected logout(): void {
    this.authService.logout();
    this.balance.set(null);
    void this.router.navigate(['/login']);
  }

  private loadBalance(): void {
    this.isBalanceLoading.set(true);

    this.apostasApiService
      .consultarSaldo()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (saldo) => {
          this.balance.set(saldo.SaldoAtual);
          this.isBalanceLoading.set(false);
        },
        error: () => {
          this.balance.set(null);
          this.isBalanceLoading.set(false);
        }
      });
  }

  private readInitialTheme(): ThemeMode {
    const storedTheme = localStorage.getItem(this.themeStorageKey);

    if (storedTheme === 'light' || storedTheme === 'dark') {
      return storedTheme;
    }

    return 'dark';
  }

  private applyTheme(theme: ThemeMode): void {
    this.document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem(this.themeStorageKey, theme);
  }
}
