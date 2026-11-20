export type Phase = 'Lobby' | 'Question' | 'Locked' | 'Reveal' | 'Finished';

export interface TimerDto {
  durationMs: number;
  remainingMs: number;
}

export interface QuestionDto {
  index: number;
  count: number;
  text: string;
  choices: string[];
  duration: number;
}

export interface RankingEntry {
  rank: number;
  playerId: string;
  pseudo: string;
  score: number;
}

export interface HostPlayer {
  id: string;
  pseudo: string;
  connected: boolean;
  hasAnswered: boolean;
}

export interface HostState {
  hasGame: boolean;
  code: string | null;
  joinUrl: string | null;
  contentTitle: string | null;
  contentError: string | null;
  phase: Phase;
  players: HostPlayer[];
  question: QuestionDto | null;
  correctAnswer: number | null;
  distribution: number[] | null;
  answersCount: number;
  timer: TimerDto | null;
  hasNextQuestion: boolean;
  scoresVisible: boolean;
  ranking: RankingEntry[] | null;
}

export interface PlayerState {
  joined: boolean;
  code: string | null;
  playerId: string | null;
  pseudo: string | null;
  phase: Phase;
  question: QuestionDto | null;
  myAnswer: number | null;
  correctAnswer: number | null;
  timer: TimerDto | null;
  scoresVisible: boolean;
  ranking: RankingEntry[] | null;
}

export interface JoinResult {
  ok: boolean;
  playerId: string | null;
  error: string | null;
}

export const LETTERS = ['A', 'B', 'C', 'D'] as const;
