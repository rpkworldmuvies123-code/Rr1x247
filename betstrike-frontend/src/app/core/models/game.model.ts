export type GameStatus = 'SCHEDULED' | 'LIVE' | 'FINISHED';
export type MatchState = 1 | 2 | 3 | 4 | 5;
export type BetMarket = '1' | 'X' | '2';

export interface Game {
  id: string;
  code: string;
  homeTeam: string;
  awayTeam: string;
  competition: string;
  startTime: string;
  status: GameStatus;
  homeScore: number;
  awayScore: number;
  estado?: number; // Estado numérico (1-5) da API
}

export interface GameDetails extends Game {
  venue?: string;
  events: ReadonlyArray<string>;
  estado?: number;
}

export interface BettingGame {
  dataDoJogo: string;
  horaDeInicio: string;
  equipaCasa: string;
  equipaFora: string;
  golosCasa: number;
  golosFora: number;
  estadoDoJogo: MatchState;
}

export interface GameFilters {
  date?: string;
  status?: GameStatus;
  competition?: string;
}

export interface CreateGameRequest {
  code: string;
  homeTeam: string;
  awayTeam: string;
  competition: string;
  startTime: string;
}

export interface UpdateGameRequest {
  status: GameStatus;
  homeScore: number;
  awayScore: number;
}

export const GAME_STATUS_LABEL: Record<GameStatus, string> = {
  SCHEDULED: 'Agendado',
  LIVE: 'Em curso',
  FINISHED: 'Finalizado'
};

export const GAME_STATE_LABEL: Record<MatchState, string> = {
  1: 'Agendado',
  2: 'Em Curso',
  3: 'Finalizado',
  4: 'Cancelado',
  5: 'Adiado'
};

export const BET_MARKET_LABEL: Record<BetMarket, string> = {
  '1': 'Vitória da equipa da casa',
  X: 'Empate',
  '2': 'Vitória da equipa visitante'
};

export const DEFAULT_BETTING_GAME: BettingGame = {
  dataDoJogo: '2026-04-21',
  horaDeInicio: '20:15',
  equipaCasa: 'Sporting CP',
  equipaFora: 'SL Benfica',
  golosCasa: 0,
  golosFora: 0,
  estadoDoJogo: 1
};

