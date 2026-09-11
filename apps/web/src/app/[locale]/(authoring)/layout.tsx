import { auth } from "@/auth";
import { AccessibilitySyncInitializer } from "@/components/settings/accessibility-sync-initializer";
import { EditorPreferencesSyncInitializer } from "@/components/settings/editor-preferences-sync-initializer";
import { ThemeSyncInitializer } from "@/components/settings/theme-sync-initializer";
import { redirect } from "@/i18n/navigation";
import { TooltipProvider } from "@game-guild/ui/components/tooltip";
import type { ReactNode } from "react";

export default async function AuthoringLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const session = await auth();
  if (!session || typeof session === "function") {
    redirect({
      href: { pathname: "/sign-in", query: { callbackUrl: "/workspace" } },
      locale,
    });
    throw new Error("Unauthenticated authoring access");
  }

  return (
    <TooltipProvider>
      <ThemeSyncInitializer />
      <AccessibilitySyncInitializer />
      <EditorPreferencesSyncInitializer />
      {children}
    </TooltipProvider>
  );
}
