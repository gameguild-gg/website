import type { Metadata } from 'next';
import SubmitBotContent from '@/components/chess/submit-bot-content';

export const metadata: Metadata = {
  title: 'Submit Bot - Chess Bot Competition',
  description: 'Upload your chess bot files for the competition',
};

export default function SubmitBotPage() {
  return <SubmitBotContent />;
}
