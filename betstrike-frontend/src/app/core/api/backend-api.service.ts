import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
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

@Injectable({
  providedIn: 'root'
})
export class BackendApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  health(): Observable<BackendHealthResponse> {
    return this.httpClient.get<BackendHealthResponse>(`${this.apiBaseUrl}/health`);
  }

  getGames(filters?: GameFilters): Observable<ReadonlyArray<Game>> {
    const params = this.buildGameFilterParams(filters);
    return this.httpClient.get<ReadonlyArray<Game>>(`${this.apiBaseUrl}/games`, { params });
  }

  getGameById(gameId: string): Observable<GameDetails> {
    return this.httpClient.get<GameDetails>(`${this.apiBaseUrl}/games/${gameId}`);
  }

  createGame(request: CreateGameRequest): Observable<Game> {
    return this.httpClient.post<Game>(`${this.apiBaseUrl}/games`, request);
  }

  updateGame(gameId: string, request: UpdateGameRequest): Observable<Game> {
    return this.httpClient.put<Game>(`${this.apiBaseUrl}/games/${gameId}`, request);
  }

  deleteGame(gameId: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiBaseUrl}/games/${gameId}`);
  }

  private buildGameFilterParams(filters?: GameFilters): HttpParams {
    if (!filters) {
      return new HttpParams();
    }

    let params = new HttpParams();

    if (filters.date) {
      params = params.set('date', filters.date);
    }

    if (filters.status) {
      params = params.set('status', filters.status);
    }

    if (filters.competition) {
      params = params.set('competition', filters.competition);
    }

    return params;
  }
}
