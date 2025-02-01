import { CatalogProvider } from '../components/catalog-context';
import { Suspense } from 'react';
import ControlBar from '../components/control-bar';
import GameCatalog from '../components/game-catalog';
import Footer from '../components/Footer';
import SpecialOffers from '@/app/[locale]/(content)/(public)/(games)/components/SpecialOffers';
import GuildPicksLoader from '../components/GuildPicksLoader';
import NewsSection from '@/app/[locale]/(content)/(public)/(games)/components/NewsSection';

function ErrorFallback({ error, resetErrorBoundary }) {
  return (
    <div role="alert">
      <p>Something went wrong:</p>
      <pre>{error.message}</pre>
      <button onClick={resetErrorBoundary}>Try again</button>
    </div>
  );
}

export default function CatalogPage() {
  return (
    <CatalogProvider>
      <div className="min-h-screen bg-[#0f1115]">
        <div className="container mx-auto px-4 py-6">
          <div className="flex flex-col gap-6">
            <main className="w-full space-y-8">
              <Suspense fallback={<div>Loading Guild Picks...</div>}>
                <GuildPicksLoader />
              </Suspense>
              <SpecialOffers />
              <ControlBar />
              <GameCatalog />
              <NewsSection />
            </main>
          </div>
        </div>
        <Footer />
      </div>
    </CatalogProvider>
  );
}


