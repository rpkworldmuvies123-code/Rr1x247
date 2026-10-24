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
  SCHEDULED: 'Scheduled',
  LIVE: 'Live',
  FINISHED: 'Finished',
  CANCELLED: 'Cancelled',
  POSTPONED: 'Postponed'
};

export const GAME_STATE_LABEL: Record<MatchState, string> = {
  1: 'Scheduled',
  2: 'Live',
  3: 'Finished',
  4: 'Cancelled',
  5: 'Postponed'
};

export const BET_MARKET_LABEL: Record<BetMarket, string> = {
  '1': 'Home win',
  X: 'Draw',
  '2': 'Away win'
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
  return (GAME_STATE_LABEL as Record<number, string>)[estado] ?? 'Unknown';
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
