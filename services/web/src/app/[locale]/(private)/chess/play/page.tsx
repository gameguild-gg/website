import type { Metadata } from 'next';
import PlayChessContent from '@/components/chess/play-chess-content';

export const metadata: Metadata = {
  title: 'Play Chess - Chess Bot Competition',
  description: 'Play chess against bots or watch bots play against each other',
};

export default function PlayPage() {
  return <PlayChessContent />;
}
