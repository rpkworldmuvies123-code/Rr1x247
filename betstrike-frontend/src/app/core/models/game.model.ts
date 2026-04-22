export type GameStatus = 'SCHEDULED' | 'LIVE' | 'FINISHED' | 'CANCELLED' | 'POSTPONED';
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
  estado: number;
}

export interface GameOdds {
  Odd1: number;
  OddX: number;
  Odd2: number;
}

export interface GameDetails extends Game {
  venue?: string;
  events: ReadonlyArray<string>;
  odds?: GameOdds;
}

export interface GameFilters {
  date?: string;
  status?: GameStatus;
  competition?: string;
  estado?: number;
}

export interface CreateGameRequest {
  code: string;
  homeTeam: string;
  awayTeam: string;
  competition: string;
  startTime: string;
}

export interface UpdateGameRequest {
  status?: GameStatus;
  homeScore?: number;
  awayScore?: number;
}

export const GAME_STATUS_LABEL: Record<GameStatus, string> = {
  SCHEDULED: 'Agendado',
  LIVE: 'Em Curso',
  FINISHED: 'Finalizado',
  CANCELLED: 'Cancelado',
  POSTPONED: 'Adiado'
};

export const GAME_STATE_LABEL: Record<MatchState, string> = {
  1: 'Agendado',
  2: 'Em Curso',
  3: 'Finalizado',
  4: 'Cancelado',
  5: 'Adiado'
};

export const BET_MARKET_LABEL: Record<BetMarket, string> = {
  '1': 'Vitória — casa',
  X: 'Empate',
  '2': 'Vitória — fora'
};

export function estadoToGameStatus(estado: number): GameStatus {
  switch (estado) {
    case 2: return 'LIVE';
    case 3: return 'FINISHED';
    case 4: return 'CANCELLED';
    case 5: return 'POSTPONED';
    default: return 'SCHEDULED';
  }
}

export function estadoLabel(estado: number): string {
  return (GAME_STATE_LABEL as Record<number, string>)[estado] ?? 'Desconhecido';
}

export function estadoClass(estado: number): string {
  switch (estado) {
    case 1: return 'agendado';
    case 2: return 'em-curso';
    case 3: return 'finalizado';
    case 4: return 'cancelado';
    case 5: return 'adiado';
    default: return 'outro';
  }
}
