import { PublicWebsiteHeader } from '@/components/app/app-shell';
import { SocialSidebar } from '@/components/feed/social-sidebar';
import { Toaster } from '@/components/ui/sonner';

export async function SocialAppShell({ children }: { children: React.ReactNode }): Promise<React.JSX.Element> {
  const header = await PublicWebsiteHeader();

  return (
    <div className="min-h-svh bg-[#050914] text-slate-100">
      <a
        href="#social-main"
        className="sr-only fixed left-4 top-4 z-50 rounded-lg bg-sky-300 px-4 py-2 text-sm font-semibold text-slate-950 focus:not-sr-only"
      >
        Skip to social feed
      </a>
      {header}
      <div className="mx-auto flex w-full max-w-[1540px] items-start">
        <SocialSidebar />
        <main id="social-main" tabIndex={-1} className="min-w-0 flex-1">
          {children}
        </main>
      </div>
      <Toaster closeButton richColors position="top-right" />
    </div>
  );
}
