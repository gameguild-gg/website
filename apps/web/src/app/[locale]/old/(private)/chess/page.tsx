import type { Metadata } from 'next';
import SummaryContent from '@/components/chess/summary-content';

export const metadata: Metadata = {
  title: 'Game Guild Chess Bot Competition Platform',
  description: 'A platform for AI chess bot competitions from the Game Guild',
};

export default function HomePage() {
  return <SummaryContent />;
}
