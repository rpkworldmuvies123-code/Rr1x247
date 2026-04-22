import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateGameRequest,
  Game,
  GameDetails,
  GameFilters,
  GameOdds,
  GameStatus,
  UpdateGameRequest,
  estadoToGameStatus
} from '../models/game.model';

export type { GameOdds };

export interface JogoListaItemResponse {
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
  Odd1?: number;
  OddX?: number;
  Odd2?: number;
  ApostasPendentes?: number;
  VolumeTotalApostado?: number;
}

export interface CriarJogoResponse {
  id: number;
  codigoJogo: string;
}


@Injectable({ providedIn: 'root' })
export class BackendApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiApostasBaseUrl;

  getGames(filters?: GameFilters): Observable<ReadonlyArray<Game>> {
    const params = this.buildParams(filters);
    return this.httpClient
      .get<ReadonlyArray<JogoListaItemResponse>>(`${this.apiBaseUrl}/jogos`, { params })
      .pipe(map((jogos) => jogos.map((j) => mapJogoToGame(j))));
  }

  getGameById(gameId: string): Observable<GameDetails> {
    return this.httpClient
      .get<JogoDetalheResponse>(`${this.apiBaseUrl}/jogos/${gameId}`)
      .pipe(map((j) => mapJogoDetalheToGameDetails(j)));
  }

  getGameOdds(gameId: string): Observable<GameOdds> {
    return this.httpClient.get<GameOdds>(`${this.apiBaseUrl}/jogos/${gameId}/odds`);
  }

  createGame(request: CreateGameRequest): Observable<CriarJogoResponse> {
    return this.httpClient.post<CriarJogoResponse>(`${this.apiBaseUrl}/jogos`, {
      codigoJogo: request.code,
      dataHoraInicio: request.startTime,
      equipaCasa: request.homeTeam,
      equipaFora: request.awayTeam,
      tipoCompeticao: request.competition
    });
  }

  updateGame(gameId: string, request: UpdateGameRequest): Observable<void> {
    const payload: Record<string, number> = {};
    if (request.status !== undefined) payload['estado'] = gameStatusToEstado(request.status);
    if (request.homeScore !== undefined) payload['golosCasa'] = request.homeScore;
    if (request.awayScore !== undefined) payload['golosVisitante'] = request.awayScore;
    return this.httpClient.patch<void>(`${this.apiBaseUrl}/jogos/${gameId}`, payload);
  }

  deleteGame(gameId: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiBaseUrl}/jogos/${gameId}`);
  }

  private buildParams(filters?: GameFilters): HttpParams {
    let params = new HttpParams();
    if (!filters) return params;
    if (filters.date) params = params.set('data', filters.date);
    if (filters.estado !== undefined) {
      params = params.set('estado', filters.estado.toString());
    } else if (filters.status) {
      params = params.set('estado', gameStatusToEstado(filters.status).toString());
    }
    if (filters.competition) params = params.set('tipoCompeticao', filters.competition);
    return params;
  }
}

function mapJogoToGame(jogo: JogoListaItemResponse): Game {
  return {
    id: jogo.CodigoJogo,
    code: jogo.CodigoJogo,
    homeTeam: jogo.EquipaCasa,
    awayTeam: jogo.EquipaFora,
    competition: jogo.TipoCompeticao,
    startTime: jogo.DataHoraInicio,
    status: estadoToGameStatus(jogo.Estado),
    homeScore: jogo.GolosCasa ?? 0,
    awayScore: jogo.GolosVisitante ?? 0,
    estado: jogo.Estado
  };
}

function mapJogoDetalheToGameDetails(jogo: JogoDetalheResponse): GameDetails {
  const odds: GameOdds | undefined =
    jogo.Odd1 != null && jogo.OddX != null && jogo.Odd2 != null
      ? { Odd1: jogo.Odd1, OddX: jogo.OddX, Odd2: jogo.Odd2 }
      : undefined;
  return { ...mapJogoToGame(jogo), venue: undefined, events: [], odds };
}

function gameStatusToEstado(status: GameStatus): number {
  switch (status) {
    case 'LIVE': return 2;
    case 'FINISHED': return 3;
    case 'CANCELLED': return 4;
    case 'POSTPONED': return 5;
    default: return 1;
  }
}
