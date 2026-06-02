import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface AnalyticsResumo {
  apostas: { ultimoMinuto: number; ultimos5Minutos: number };
  utilizadoresAtivos5Min: number;
  taxasVitoria: { ganhou1: number; ganhouX: number; ganhou2: number; perdeu: number; total: number };
  volumePorCompeticao: Record<string, number>;
  exposicaoFinanceira: Record<string, number>;
  volumeTotal: number;
  margemPlataforma: number;
  timestamp: string;
}

export interface AnalyticsJogo {
  codigoJogo: string;
  volumeApostado: number;
  exposicaoFinanceira: number;
  timestamp: string;
}

export interface Alerta {
  timestamp: string;
  tipo: string;
  mensagem: string;
  codigoJogo: string | null;
}

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/analytics';

  getResumo(): Observable<AnalyticsResumo> {
    return this.http.get<AnalyticsResumo>(`${this.base}/resumo`);
  }

  getJogo(codigoJogo: string): Observable<AnalyticsJogo> {
    return this.http.get<AnalyticsJogo>(`${this.base}/jogos/${codigoJogo}`);
  }

  getAlertas(): Observable<Alerta[]> {
    return this.http.get<Alerta[]>(`${this.base}/alertas`);
  }
}
