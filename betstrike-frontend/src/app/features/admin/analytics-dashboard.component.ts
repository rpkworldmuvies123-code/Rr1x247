import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe, DecimalPipe, KeyValuePipe } from '@angular/common';
import { interval, startWith, switchMap } from 'rxjs';
import { Alerta, AnalyticsResumo, AnalyticsService } from '../../core/api/analytics.service';

@Component({
  selector: 'app-analytics-dashboard',
  imports: [DatePipe, DecimalPipe, KeyValuePipe],
  templateUrl: './analytics-dashboard.component.html',
  styleUrl: './analytics-dashboard.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnalyticsDashboardComponent implements OnInit {
  private readonly analyticsService = inject(AnalyticsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly resumo = signal<AnalyticsResumo | null>(null);
  protected readonly alertas = signal<Alerta[]>([]);
  protected readonly lastUpdate = signal<Date | null>(null);
  protected readonly error = signal('');

  ngOnInit() {
    interval(5000).pipe(
      startWith(0),
      switchMap(() => this.analyticsService.getResumo()),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: data => { this.resumo.set(data); this.lastUpdate.set(new Date()); this.error.set(''); },
      error: () => this.error.set('Não foi possível ligar ao AnalyticsService.'),
    });

    interval(10000).pipe(
      startWith(0),
      switchMap(() => this.analyticsService.getAlertas()),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({ next: data => this.alertas.set(data) });
  }

  protected taxaVitoriaPercent(wins: number, total: number): string {
    if (total === 0) return '—';
    return `${((wins / total) * 100).toFixed(1)}%`;
  }

  protected exposicaoTotal(exposicao: Record<string, number>): number {
    return Object.values(exposicao).reduce((a, b) => a + b, 0);
  }
}
