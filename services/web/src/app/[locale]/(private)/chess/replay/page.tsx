import { redirect } from 'next/navigation';
import type { Metadata } from 'next';
import ChessReplayContent from '@/components/chess/chess-replay-content';

export const metadata: Metadata = {
  title: 'Replay Match - Chess Bot Competition',
  description: 'Replay and analyze chess matches between bots',
};

interface ReplayPageProps {
  searchParams: { [key: string]: string | string[] | undefined };
}

export default function ReplayPage({ searchParams }: ReplayPageProps) {
  const matchId = searchParams.matchid as string;

  // If no match ID is provided, redirect to the matches page
  if (!matchId) {
    redirect('/matches');
  }

  return <ChessReplayContent matchId={matchId} />;
}
