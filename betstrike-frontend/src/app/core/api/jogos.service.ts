import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';

export interface JogoListaItemResponse {
  id: number;
  CodigoJogo: string;
  DataHoraInicio: string;
  EquipaCasa: string;
  EquipaFora: string;
  TipoCompeticao: string;
  Estado: number;
  GolosCasa: number;
  GolosVisitante: number;
}

export interface JogoDetalheResponse {
  id: number;
  CodigoJogo: string;
  DataHoraInicio: string;
  EquipaCasa: string;
  EquipaFora: string;
  TipoCompeticao: string;
  Estado: number;
  GolosCasa: number;
  GolosVisitante: number;
  ApostasPendentes: number;
  VolumeTotalApostado: number;
}

export interface CriarJogoResponse {
  id: number;
  codigoJogo: string;
}

@Injectable({
  providedIn: 'root'
})
export class JogosService {
  private readonly httpClient = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly apiBaseUrl = environment.apiApostasBaseUrl;

  /**
   * Obtém a lista de jogos com filtros opcionais
   */
  listar(filtros?: {
    dataInicio?: string;
    dataFim?: string;
    estado?: number;
  }): Observable<ReadonlyArray<JogoListaItemResponse>> {
    let params = new HttpParams();

    const data = filtros?.dataInicio || filtros?.dataFim;
    if (data) {
      params = params.set('data', data);
    }

    if (filtros?.estado !== undefined) {
      params = params.set('estado', filtros.estado.toString());
    }

    return this.httpClient
      .get<ReadonlyArray<JogoListaItemResponse>>(`${this.apiBaseUrl}/jogos`, { params })
      .pipe(catchError(this.handleError));
  }

  /**
   * Obtém os detalhes de um jogo pelo código
   */
  obterPorCodigo(codigoJogo: string): Observable<JogoDetalheResponse> {
    return this.httpClient
      .get<JogoDetalheResponse>(`${this.apiBaseUrl}/jogos/${codigoJogo}`)
      .pipe(catchError(this.handleError));
  }

  /**
   * Cria um novo jogo
   */
  criar(dados: {
      codigoJogo: string;
      dataHoraInicio: string;
      equipaCasa: string;
      equipaFora: string;
      tipoCompeticao: string;
  }): Observable<CriarJogoResponse> {
    const payload = {
      CodigoJogo: dados.codigoJogo,
      DataHoraInicio: dados.dataHoraInicio,
      EquipaCasa: dados.equipaCasa,
      EquipaFora: dados.equipaFora,
      TipoCompeticao: dados.tipoCompeticao
    };

    return this.httpClient
      .post<CriarJogoResponse>(`${this.apiBaseUrl}/jogos`, payload, this.buildWriteOptions())
      .pipe(catchError(this.handleError));
  }

  /**
   * Atualiza o estado e/ou marcador de um jogo
   */
  atualizar(
    codigoJogo: string,
    dados: {
      estadoAtual?: number;
      golosCasa?: number;
      golosVisitante?: number;
    }
  ): Observable<void> {
    const payload: Record<string, number> = {};

    if (dados.estadoAtual !== undefined) {
      payload['Estado'] = dados.estadoAtual;
    }

    if (dados.golosCasa !== undefined) {
      payload['GolosCasa'] = dados.golosCasa;
    }

    if (dados.golosVisitante !== undefined) {
      payload['GolosVisitante'] = dados.golosVisitante;
    }

    return this.httpClient.patch<void>(`${this.apiBaseUrl}/jogos/${codigoJogo}`, payload, this.buildWriteOptions()).pipe(catchError(this.handleError));
  }

  /**
   * Remove um jogo
   */
  remover(codigoJogo: string): Observable<void> {
    return this.httpClient
      .delete<void>(`${this.apiBaseUrl}/jogos/${codigoJogo}`, this.buildWriteOptions())
      .pipe(catchError(this.handleError));
  }

  private buildWriteOptions(): { headers?: HttpHeaders } {
    let headers = new HttpHeaders();

    const token = this.authService.currentSession()?.token;
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
    }

    return headers.keys().length > 0 ? { headers } : {};
  }

  private handleError(error: any): Observable<never> {
    console.error('Erro na API de Apostas:', error);

    let errorMessage = 'Erro ao comunicar com a API de Apostas';

    if (error.error instanceof ErrorEvent) {
      // Erro no cliente
      errorMessage = error.error.message;
    } else if (error.status) {
      // Erro do servidor
      errorMessage =
        error.error?.message || `Erro HTTP ${error.status}: ${error.statusText}`;
    }

    return throwError(() => new Error(errorMessage));
  }
}

