import { PublicWebsiteHeader } from '@/components/app/app-shell';
import { SocialSidebar, SocialSidebarToggle } from '@/components/feed/social-sidebar';
import { Toaster } from '@/components/ui/sonner';
import { SidebarProvider } from '@game-guild/ui/components/sidebar';

export async function SocialAppShell({ children }: { children: React.ReactNode }): Promise<React.JSX.Element> {
  const header = await PublicWebsiteHeader({
    embedded: true,
    leading: <SocialSidebarToggle placement="header" />,
  });

  return (
    <div className="min-h-svh bg-[#080d18] text-slate-100">
      <a
        href="#social-main"
        className="sr-only fixed left-4 top-4 z-50 rounded-lg bg-sky-300 px-4 py-2 text-sm font-semibold text-slate-950 focus:not-sr-only"
      >
        Skip to social feed
      </a>
      <SidebarProvider
        className="min-h-svh bg-[#080d18]"
        style={
          {
            '--sidebar-width': '15rem',
            '--sidebar-width-icon': '4rem',
          } as React.CSSProperties
        }
      >
        <SocialSidebar />
        <div className="flex min-h-svh min-w-0 flex-1 flex-col">
          {header}
          <main id="social-main" tabIndex={-1} className="min-w-0 flex-1 overflow-x-hidden">
            {children}
          </main>
        </div>
      </SidebarProvider>
      <Toaster closeButton richColors position="top-right" />
    </div>
  );
}
