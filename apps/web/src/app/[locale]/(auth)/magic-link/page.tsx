import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import {
  MagicLinkFlow,
  type MagicLinkMessages,
} from "@/components/magic-link-flow";
import { resolveAllowedAuthRedirect } from "@/lib/auth/cross-domain-auth";

export const metadata: Metadata = {
  referrer: "no-referrer",
  robots: { index: false, follow: false },
};

export default async function Page({
  params,
  searchParams,
}: PageProps<"/[locale]/magic-link">): Promise<React.JSX.Element> {
  const [{ locale }, query, t] = await Promise.all([
    params,
    searchParams,
    getTranslations("magicLink"),
  ]);
  const rawToken = Array.isArray(query?.token) ? query.token[0] : query?.token;
  const token =
    typeof rawToken === "string" && rawToken.trim().length > 0
      ? rawToken
      : null;
  const rawRedirect = Array.isArray(query?.redirectTo)
    ? query.redirectTo[0]
    : query?.redirectTo;
  const redirectTo = resolveAllowedAuthRedirect(
    typeof rawRedirect === "string" ? rawRedirect : undefined,
  );
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  const messages: MagicLinkMessages = {
    request: {
      title: t("request.title"),
      description: t("request.description"),
      emailLabel: t("request.emailLabel"),
      emailPlaceholder: t("request.emailPlaceholder"),
      submit: t("request.submit"),
      pending: t("request.pending"),
      successTitle: t("request.successTitle"),
      successDescription: t("request.successDescription"),
      error: t("request.error"),
    },
    consume: {
      title: t("consume.title"),
      pending: t("consume.pending"),
      success: t("consume.success"),
      errorTitle: t("consume.errorTitle"),
      errorDescription: t("consume.errorDescription"),
    },
    backToSignIn: t("backToSignIn"),
    requestAnother: t("requestAnother"),
  };

  return (
    <MagicLinkFlow
      token={token}
      redirectTo={redirectTo}
      locale={locale}
      apiUrl={apiUrl}
      messages={messages}
    />
  );
}
