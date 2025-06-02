import type { Metadata } from 'next';
import MatchesContent from '@/components/chess/matches-content';

export const metadata: Metadata = {
  title: 'Matches - Chess Bot Competition',
  description: 'View recent matches between chess bots',
};

export default function MatchesPage() {
  return <MatchesContent />;
}
