import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';
import {
  CreateGameRequest,
  Game,
  GameDetails,
  GameFilters,
  UpdateGameRequest
} from '../models/game.model';

export interface BackendHealthResponse {
  message: string;
}

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

export interface JogoDetalheResponse extends JogoListaItemResponse {
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
export class BackendApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly apiBaseUrl = environment.apiApostasBaseUrl;

  getGames(filters?: GameFilters): Observable<ReadonlyArray<Game>> {
    const params = this.buildGameFilterParams(filters);
    return this.httpClient
      .get<ReadonlyArray<JogoListaItemResponse>>(`${this.apiBaseUrl}/jogos`, { params })
      .pipe(map((jogos) => jogos.map((jogo) => this.mapJogoToGame(jogo))));
  }

  getGameById(gameId: string): Observable<GameDetails> {
    return this.httpClient
      .get<JogoDetalheResponse>(`${this.apiBaseUrl}/jogos/${gameId}`)
      .pipe(map((jogo) => this.mapJogoDetalheToGameDetails(jogo)));
  }

  createGame(request: CreateGameRequest): Observable<Game> {
    const payload = {
      CodigoJogo: request.code,
      DataHoraInicio: request.startTime,
      EquipaCasa: request.homeTeam,
      EquipaFora: request.awayTeam,
      TipoCompeticao: request.competition
    };

    return this.httpClient
      .post<CriarJogoResponse>(`${this.apiBaseUrl}/jogos`, payload, this.buildWriteOptions())
      .pipe(map((jogo) => this.mapCriarJogoToGame(jogo, request)));
  }

  updateGame(gameId: string, request: UpdateGameRequest): Observable<void> {
    const payload = {
      Estado: this.mapGameStatusToEstado(request.status),
      GolosCasa: request.homeScore,
      GolosVisitante: request.awayScore
    };

    return this.httpClient.patch<void>(`${this.apiBaseUrl}/jogos/${gameId}`, payload, this.buildWriteOptions());
  }

  deleteGame(gameId: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiBaseUrl}/jogos/${gameId}`, this.buildWriteOptions());
  }

  private buildGameFilterParams(filters?: GameFilters): HttpParams {
    if (!filters) {
      return new HttpParams();
    }

    let params = new HttpParams();

    if (filters.date) {
      params = params.set('data', filters.date);
    }

    if (filters.status) {
      const estado = this.mapGameStatusToEstado(filters.status);
      params = params.set('estado', estado.toString());
    }

    if (filters.competition) {
      params = params.set('tipoCompeticao', filters.competition);
    }

    return params;
  }

  private mapJogoToGame(jogo: JogoListaItemResponse): Game {
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
      estado: jogo.Estado
    };
  }

  private mapJogoDetalheToGameDetails(jogo: JogoDetalheResponse): GameDetails {
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

  private mapCriarJogoToGame(response: CriarJogoResponse, request: CreateGameRequest): Game {
    return {
      id: response.codigoJogo,
      code: response.codigoJogo,
      homeTeam: request.homeTeam,
      awayTeam: request.awayTeam,
      competition: request.competition,
      startTime: new Date(request.startTime).toISOString(),
      status: 'SCHEDULED',
      homeScore: 0,
      awayScore: 0,
      estado: 1
    };
  }

  private buildWriteOptions(): { headers?: HttpHeaders } {
    let headers = new HttpHeaders();

    const token = this.authService.currentSession()?.token;
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
    }

    return headers.keys().length > 0 ? { headers } : {};
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
        return 'SCHEDULED'; // Cancelado e Adiado aparecem como agendado
      default:
        return 'SCHEDULED';
    }
  }

  private mapGameStatusToEstado(status: 'SCHEDULED' | 'LIVE' | 'FINISHED'): number {
    switch (status) {
      case 'SCHEDULED':
        return 1;
      case 'LIVE':
        return 2;
      case 'FINISHED':
        return 3;
      default:
        return 1;
    }
  }
}
