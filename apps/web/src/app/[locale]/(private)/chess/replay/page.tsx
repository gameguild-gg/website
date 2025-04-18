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
  // Add extensive debugging for the searchParams
  console.log('[ReplayPage] Component rendering');
  console.log('[ReplayPage] searchParams:', JSON.stringify(searchParams, null, 2));
  console.log('[ReplayPage] searchParams type:', typeof searchParams);
  console.log('[ReplayPage] searchParams keys:', Object.keys(searchParams));

  // Check if matchid exists in searchParams
  const hasMatchId = 'matchid' in searchParams;
  console.log('[ReplayPage] Has matchid in searchParams:', hasMatchId);

  // Get the matchId and log its value and type
  const matchId = searchParams.matchid as string;
  console.log('[ReplayPage] matchId value:', matchId);
  console.log('[ReplayPage] matchId type:', typeof matchId);

  // Check if matchId is empty or undefined
  const isMatchIdEmpty = !matchId || matchId.trim() === '';
  console.log('[ReplayPage] Is matchId empty or undefined:', isMatchIdEmpty);

  // If no match ID is provided, redirect to the matches page
  if (!matchId) {
    console.log('[ReplayPage] No matchId provided, redirecting to matches page');
    redirect('/chess/matches');
    return null; // Add return to ensure no further execution
  }

  console.log('[ReplayPage] matchId is valid, rendering ChessReplayContent with matchId:', matchId);

  return <ChessReplayContent matchId={matchId} />;
}
