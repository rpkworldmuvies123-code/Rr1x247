import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

type ThemeMode = 'dark' | 'light';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly themeStorageKey = 'betstrike.theme';

  protected readonly isAuthenticated = computed(() => this.authService.isAuthenticated());
  protected readonly isAdmin = computed(() => this.authService.isAdmin());
  protected readonly username = computed(() => this.authService.currentSession()?.username ?? '');
  protected readonly isAdminMode = computed(() => this.isAuthenticated() && this.isAdmin());
  protected readonly themeMode = signal<ThemeMode>(this.readInitialTheme());
  protected readonly isLightMode = computed(() => this.themeMode() === 'light');

  constructor() {
    this.applyTheme(this.themeMode());
  }


  protected toggleTheme(): void {
    const nextTheme: ThemeMode = this.themeMode() === 'dark' ? 'light' : 'dark';
    this.themeMode.set(nextTheme);
    this.applyTheme(nextTheme);
  }

  protected logout(): void {
    this.authService.logout();
    void this.router.navigate(['/login']);
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
