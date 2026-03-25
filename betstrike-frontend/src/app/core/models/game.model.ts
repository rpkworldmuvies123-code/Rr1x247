export type GameStatus = 'SCHEDULED' | 'LIVE' | 'FINISHED';

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
}

export interface GameDetails extends Game {
  venue?: string;
  events: ReadonlyArray<string>;
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

