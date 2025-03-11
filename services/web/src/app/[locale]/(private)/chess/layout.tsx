import './styles.css';
import type { ReactNode } from 'react';
import { SideNavigation } from '@/components/chess/side-navigation';
import { SidebarInset, SidebarProvider } from '@/components/chess/ui/sidebar';

interface ChessCompetitionLayoutProps {
  children: ReactNode;
}

export function ChessCompetitionLayout({ children }: ChessCompetitionLayoutProps) {
  const defaultOpen = true;

  return (
    <SidebarProvider defaultOpen={defaultOpen}>
      <div className="flex min-h-screen">
        <SideNavigation />
        <SidebarInset>
          <main className="flex-1 p-4 md:p-6 lg:p-8">{children}</main>
        </SidebarInset>
      </div>
    </SidebarProvider>
  );
}

export default ChessCompetitionLayout;
