"use client";

import { type FormEvent, useSyncExternalStore } from "react";
import { useTranslations } from "next-intl";
import { useAuth } from "@game-guild/client/react";
import { Link } from "@/i18n/navigation";
import { Button } from "@game-guild/ui/components/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card";
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@game-guild/ui/components/field";
import { Input } from "@game-guild/ui/components/input";

const subscribeToHydration = () => () => undefined;

/**
 * MFA challenge form for the dedicated /sign-in/2fa route. The challenge
 * (mfaToken, availableMethods) lives in SessionProvider memory through
 * useAuth and survives the in-app navigation from /sign-in; without one
 * (direct visit or post-expiry refresh) a back-to-sign-in notice renders.
 */
export function MfaChallengeForm({
  className,
  redirectTo = "/",
  ...props
}: React.ComponentProps<"div"> & {
  redirectTo?: string;
}) {
  const { signIn, isLoading, error, mfaChallenge, clearMfa } = useAuth();
  const t = useTranslations("signIn.mfa");
  const isHydrated = useSyncExternalStore(
    subscribeToHydration,
    () => true,
    () => false,
  );

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!mfaChallenge) return;
    const form = e.currentTarget;
    const data = new FormData(form);
    try {
      await signIn("credentials", {
        mfaToken: mfaChallenge.mfaToken,
        method: data.get("method") || "Totp",
        code: data.get("code") as string,
        redirectTo,
      });
      form.reset();
    } catch {
      /* The hook keeps the current challenge and exposes the error. */
    }
  }

  if (!mfaChallenge) {
    return (
      <Card className={className} {...props}>
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">
            <h1>{t("title")}</h1>
          </CardTitle>
          <CardDescription className="text-slate-300">
            {t("noChallenge")}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Link
            href="/sign-in"
            className="text-sky-200 underline-offset-4 hover:underline"
          >
            {t("backToSignIn")}
          </Link>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className={className} {...props}>
      <Card className="border-white/10 bg-slate-900/85 text-white shadow-2xl shadow-sky-950/30 backdrop-blur">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">
            <h1>{t("title")}</h1>
          </CardTitle>
          <CardDescription className="text-slate-300">
            {t("description")}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            method="post"
            onSubmit={(event) => {
              void handleSubmit(event);
            }}
            data-auth-ready={isHydrated ? "true" : "false"}
          >
            <FieldGroup>
              {mfaChallenge.availableMethods.includes("BackupCode") && (
                <Field>
                  <FieldLabel htmlFor="mfa-method">
                    {t("methodLabel")}
                  </FieldLabel>
                  <select
                    id="mfa-method"
                    name="method"
                    defaultValue="Totp"
                    disabled={isLoading}
                    className="h-10 rounded border border-white/10 bg-slate-900 px-3"
                  >
                    <option value="Totp">{t("methodTotp")}</option>
                    <option value="BackupCode">{t("methodBackupCode")}</option>
                  </select>
                </Field>
              )}
              <Field>
                <FieldLabel htmlFor="mfa-code">{t("codeLabel")}</FieldLabel>
                <Input
                  id="mfa-code"
                  name="code"
                  autoComplete="one-time-code"
                  required
                  disabled={isLoading}
                  className="h-10 border-white/10 bg-white/5 text-white"
                />
              </Field>
              {error &&
                !("type" in error && error.type === "MfaRequired") && (
                  <FieldError>{error.message}</FieldError>
                )}
              <Button type="submit" disabled={isLoading || !isHydrated}>
                {t("submit")}
              </Button>
              <Button
                type="button"
                variant="ghost"
                disabled={isLoading}
                className="text-slate-200 hover:bg-white/10 hover:text-white"
                onClick={() => {
                  clearMfa();
                }}
              >
                {t("startAgain")}
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
