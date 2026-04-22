import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CriarApostaRequest {
  codigoJogo: string;
  tipoAposta: '1' | 'X' | '2';
  montanteApostado: number;
}

export interface CriarApostaResponse {
  id: number;
}

export interface ApostaResponse {
  Id: number;
  JogoId: number;
  CodigoJogo: string;
  EquipaCasa: string;
  EquipaFora: string;
  UtilizadorId: string;
  TipoAposta: '1' | 'X' | '2';
  MontanteApostado: number;
  OddMomento: number;
  Estado: number;
  DataHoraRegisto: string;
  PremioPotencial: number;
}

export interface SaldoResponse {
  UtilizadorId: string;
  SaldoAtual: number;
  DataHoraAtualizacao: string;
}

export interface ResultadoResponse {
  Id: number;
  JogoId: number;
  CodigoJogo: string;
  EquipaCasa: string;
  EquipaFora: string;
  GolosCasa: number;
  GolosVisitante: number;
  DataHoraAtualizacao: string;
}

export interface UtilizadorAdminResponse {
  Id: string;
  NomeCompleto: string;
  Email: string;
  DataRegisto: string;
  SaldoAtual: number | null;
  SaldoAtualizadoEm: string | null;
  IsAdmin: boolean;
}

export interface LogAdminEntry {
  Id: number;
  AdminId: string;
  AdminEmail: string;
  UtilizadorId: string | null;
  UtilizadorEmail: string | null;
  Acao: string;
  Detalhe: string | null;
  DataHora: string;
}

export interface EstatisticasJogoResponse {
  CodigoJogo: string;
  TotalApostado: number;
  TotalApostas: number;
  Apostas1: number;
  ApostasX: number;
  Apostas2: number;
  ApostasPendentes: number;
  ApostasGanhas: number;
  ApostasPerdidas: number;
  ApostasAnuladas: number;
  MargemPlataforma: number;
}

@Injectable({
  providedIn: 'root'
})
export class ApostasApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiApostasBaseUrl;

  criarAposta(request: CriarApostaRequest): Observable<CriarApostaResponse> {
    return this.httpClient.post<CriarApostaResponse>(`${this.apiBaseUrl}/apostas`, {
      codigoJogo: request.codigoJogo,
      tipoAposta: request.tipoAposta,
      montanteApostado: request.montanteApostado
    });
  }

  listarApostas(filters?: {
    codigoJogo?: string;
    estado?: number;
    dataInicio?: string;
    dataFim?: string;
  }): Observable<ReadonlyArray<ApostaResponse>> {
    let params = new HttpParams();

    if (filters?.codigoJogo) {
      params = params.set('codigoJogo', filters.codigoJogo);
    }

    if (filters?.estado !== undefined) {
      params = params.set('estado', filters.estado.toString());
    }

    if (filters?.dataInicio) {
      params = params.set('dataInicio', filters.dataInicio);
    }

    if (filters?.dataFim) {
      params = params.set('dataFim', filters.dataFim);
    }

    return this.httpClient.get<ReadonlyArray<ApostaResponse>>(`${this.apiBaseUrl}/apostas`, { params });
  }

  cancelarAposta(apostaId: number): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiBaseUrl}/apostas/${apostaId}`);
  }

  consultarSaldo(): Observable<SaldoResponse> {
    return this.httpClient.get<SaldoResponse>(`${this.apiBaseUrl}/pagamentos/saldo`);
  }

  depositarSaldo(valor: number): Observable<SaldoResponse> {
    return this.httpClient.post<SaldoResponse>(`${this.apiBaseUrl}/pagamentos/depositar`, { valor });
  }

  consultarResultado(codigoJogo: string): Observable<ResultadoResponse> {
    return this.httpClient.get<ResultadoResponse>(`${this.apiBaseUrl}/resultados/${codigoJogo}`);
  }

  consultarEstatisticasJogo(codigoJogo: string): Observable<EstatisticasJogoResponse> {
    return this.httpClient.get<EstatisticasJogoResponse>(`${this.apiBaseUrl}/estatisticas/jogos/${codigoJogo}`);
  }

  // Admin
  consultarUtilizadoresAdmin(): Observable<ReadonlyArray<UtilizadorAdminResponse>> {
    return this.httpClient.get<ReadonlyArray<UtilizadorAdminResponse>>(`${this.apiBaseUrl}/admin/utilizadores`);
  }

  ajustarSaldoAdmin(utilizadorId: string, valor: number, motivo: string): Observable<SaldoResponse> {
    return this.httpClient.post<SaldoResponse>(
      `${this.apiBaseUrl}/admin/utilizadores/${encodeURIComponent(utilizadorId)}/saldo`,
      { valor, motivo }
    );
  }

  consultarTransacoes(params?: { tipo?: string; apostaId?: number; dataInicio?: string; dataFim?: string; pagina?: number }): Observable<ReadonlyArray<{
    Id: number; ApostaId: number | null; UtilizadorId: string;
    TipoTransacao: string; Valor: number; DataHora: string; Estado: string;
  }>> {
    let httpParams = new HttpParams();
    if (params?.tipo) httpParams = httpParams.set('tipo', params.tipo);
    if (params?.apostaId) httpParams = httpParams.set('apostaId', params.apostaId.toString());
    if (params?.dataInicio) httpParams = httpParams.set('dataInicio', params.dataInicio);
    if (params?.dataFim) httpParams = httpParams.set('dataFim', params.dataFim);
    if (params?.pagina) httpParams = httpParams.set('pagina', params.pagina.toString());
    return this.httpClient.get<any[]>(`${this.apiBaseUrl}/pagamentos/transacoes`, { params: httpParams });
  }

  consultarLogAdmin(params?: { adminId?: string; utilizadorId?: string; pagina?: number }): Observable<ReadonlyArray<LogAdminEntry>> {
    let httpParams = new HttpParams();
    if (params?.adminId) httpParams = httpParams.set('adminId', params.adminId);
    if (params?.utilizadorId) httpParams = httpParams.set('utilizadorId', params.utilizadorId);
    if (params?.pagina) httpParams = httpParams.set('pagina', params.pagina.toString());
    return this.httpClient.get<ReadonlyArray<LogAdminEntry>>(`${this.apiBaseUrl}/admin/log`, { params: httpParams });
  }
}

