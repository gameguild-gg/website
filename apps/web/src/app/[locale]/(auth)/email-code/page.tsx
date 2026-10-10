import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import {
  EmailCodeFlow,
  type EmailCodeMessages,
} from "@/components/email-code-flow";
import { resolveAllowedAuthRedirect } from "@/lib/auth/cross-domain-auth";

export const metadata: Metadata = {
  referrer: "no-referrer",
  robots: { index: false, follow: false },
};

export default async function Page({
  searchParams,
}: PageProps<"/[locale]/email-code">): Promise<React.JSX.Element> {
  const [query, t] = await Promise.all([
    searchParams,
    getTranslations("emailCode"),
  ]);
  const rawRedirect = Array.isArray(query?.redirectTo)
    ? query.redirectTo[0]
    : query?.redirectTo;
  const redirectTo = resolveAllowedAuthRedirect(
    typeof rawRedirect === "string" ? rawRedirect : undefined,
  );
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  const messages: EmailCodeMessages = {
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
      description: t("consume.description"),
      codeLabel: t("consume.codeLabel"),
      codePlaceholder: t("consume.codePlaceholder"),
      submit: t("consume.submit"),
      pending: t("consume.pending"),
      useDifferentEmail: t("consume.useDifferentEmail"),
      errorTitle: t("consume.errorTitle"),
      error: t("consume.error"),
    },
    backToSignIn: t("backToSignIn"),
  };

  return (
    <EmailCodeFlow redirectTo={redirectTo} apiUrl={apiUrl} messages={messages} />
  );
}
