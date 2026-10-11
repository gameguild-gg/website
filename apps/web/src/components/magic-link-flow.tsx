"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "@game-guild/client/react";
import { Link } from "@/i18n/navigation";
import { requestMagicLink } from "@/lib/auth/magic-link-request";
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
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@game-guild/ui/components/field";
import { Input } from "@game-guild/ui/components/input";
import { AlertCircle, CheckCircle2, Loader2 } from "lucide-react";
import { SignInForm } from "./sign-in-form";

export interface MagicLinkMessages {
  request: {
    title: string;
    description: string;
    emailLabel: string;
    emailPlaceholder: string;
    submit: string;
    pending: string;
    successTitle: string;
    successDescription: string;
    error: string;
  };
  consume: {
    title: string;
    pending: string;
    success: string;
    errorTitle: string;
    errorDescription: string;
  };
  backToSignIn: string;
  requestAnother: string;
}

export function MagicLinkFlow({
  token,
  redirectTo = "/",
  apiUrl,
  messages,
}: {
  token: string | null;
  redirectTo?: string;
  apiUrl: string;
  messages: MagicLinkMessages;
}) {
  return token ? (
    <MagicLinkConsumer
      token={token}
      redirectTo={redirectTo}
      messages={messages}
    />
  ) : (
    <MagicLinkRequestForm apiUrl={apiUrl} messages={messages} />
  );
}

function MagicLinkRequestForm({
  apiUrl,
  messages,
}: {
  apiUrl: string;
  messages: MagicLinkMessages;
}) {
  const [email, setEmail] = useState("");
  const [pending, setPending] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [failed, setFailed] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setFailed(false);

    try {
      const sent = await requestMagicLink(email, apiUrl);
      if (!sent) {
        setFailed(true);
        return;
      }

      setSubmitted(true);
    } catch {
      setFailed(true);
    } finally {
      setPending(false);
    }
  }

  return (
    <Card className="border-white/10 bg-slate-900/85 text-white shadow-2xl shadow-sky-950/30 backdrop-blur">
      <CardHeader className="text-center">
        <CardTitle className="text-2xl">
          <h1>
            {submitted ? messages.request.successTitle : messages.request.title}
          </h1>
        </CardTitle>
        <CardDescription
          className="text-slate-300"
          role={submitted ? "status" : undefined}
        >
          {submitted
            ? messages.request.successDescription
            : messages.request.description}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {submitted ? (
          <div className="flex flex-col gap-5">
            <CheckCircle2
              className="mx-auto size-5 text-emerald-200"
              aria-hidden="true"
            />
            <Link
              href="/sign-in"
              className="text-center text-sky-200 underline-offset-4 hover:underline"
            >
              {messages.backToSignIn}
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="magic-link-email">
                  {messages.request.emailLabel}
                </FieldLabel>
                <Input
                  id="magic-link-email"
                  name="email"
                  type="email"
                  placeholder={messages.request.emailPlaceholder}
                  autoComplete="email"
                  required
                  value={email}
                  disabled={pending}
                  onChange={(event) => setEmail(event.currentTarget.value)}
                  className="h-10 border-white/10 bg-white/5 text-white placeholder:text-slate-500"
                  aria-invalid={failed}
                  aria-describedby={
                    failed ? "magic-link-request-error" : undefined
                  }
                />
                {failed ? (
                  <FieldError id="magic-link-request-error" role="alert">
                    <AlertCircle
                      className="mr-1 inline size-4"
                      aria-hidden="true"
                    />
                    {messages.request.error}
                  </FieldError>
                ) : null}
              </Field>
              <Field>
                <Button
                  type="submit"
                  size="lg"
                  disabled={pending || !email.trim()}
                >
                  {pending ? (
                    <Loader2
                      className="mr-2 size-4 animate-spin"
                      aria-hidden="true"
                    />
                  ) : null}
                  {pending ? messages.request.pending : messages.request.submit}
                </Button>
                <FieldDescription className="text-center text-slate-300">
                  <Link
                    href="/sign-in"
                    className="text-sky-200 underline-offset-4 hover:underline"
                  >
                    {messages.backToSignIn}
                  </Link>
                </FieldDescription>
              </Field>
            </FieldGroup>
          </form>
        )}
      </CardContent>
    </Card>
  );
}

function MagicLinkConsumer({
  token,
  redirectTo,
  messages,
}: {
  token: string;
  redirectTo: string;
  messages: MagicLinkMessages;
}) {
  const { signIn, mfaChallenge, mfaEnrollmentBackupCodes } = useAuth();
  const attemptedToken = useRef<string | null>(null);
  const [status, setStatus] = useState<"pending" | "complete" | "failed">(
    "pending",
  );

  useEffect(() => {
    if (attemptedToken.current === token) return;
    attemptedToken.current = token;

    let active = true;
    void signIn("credentials", { magicLinkToken: token, redirectTo })
      .then(() => {
        if (active) setStatus("complete");
      })
      .catch(() => {
        if (active) setStatus("failed");
      });

    return () => {
      active = false;
    };
  }, [redirectTo, signIn, token]);

  if (mfaChallenge || mfaEnrollmentBackupCodes?.length) {
    return <SignInForm redirectTo={redirectTo} />;
  }

  return (
    <Card className="border-white/10 bg-slate-900/85 text-white shadow-2xl shadow-sky-950/30 backdrop-blur">
      <CardHeader className="text-center">
        <CardTitle className="text-2xl">
          <h1>
            {status === "failed"
              ? messages.consume.errorTitle
              : messages.consume.title}
          </h1>
        </CardTitle>
        <CardDescription
          className="text-slate-300"
          role={status === "failed" ? "alert" : "status"}
        >
          {status === "failed"
            ? messages.consume.errorDescription
            : status === "complete"
              ? messages.consume.success
              : messages.consume.pending}
        </CardDescription>
      </CardHeader>
      {status === "failed" ? (
        <CardContent className="flex flex-col gap-4 text-center">
          <Link
            href="/magic-link"
            className="text-sky-200 underline-offset-4 hover:underline"
          >
            {messages.requestAnother}
          </Link>
          <Link
            href="/sign-in"
            className="text-slate-300 underline-offset-4 hover:underline"
          >
            {messages.backToSignIn}
          </Link>
        </CardContent>
      ) : (
        <CardContent>
          <p
            aria-hidden="true"
            className="flex items-center justify-center gap-2 text-sm text-slate-200"
          >
            {status === "complete" ? (
              <CheckCircle2
                className="size-4 text-emerald-200"
                aria-hidden="true"
              />
            ) : (
              <Loader2 className="size-4 animate-spin" aria-hidden="true" />
            )}
          </p>
        </CardContent>
      )}
    </Card>
  );
}
