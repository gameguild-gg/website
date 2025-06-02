import type { Metadata } from 'next';
import LeaderboardContent from '@/components/chess/leaderboard-content';

export const metadata: Metadata = {
  title: 'Leaderboard - Chess Bot Competition',
  description: 'View the current rankings of chess bots in the competition',
};

export default function LeaderboardPage() {
  return <LeaderboardContent />;
}
