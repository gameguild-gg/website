"use client";

import { useState, type FormEvent } from "react";
import { useAuth } from "@game-guild/client/react";
import { Link } from "@/i18n/navigation";
import { requestEmailCode } from "@/lib/auth/email-code-request";
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
import { AlertCircle, Loader2 } from "lucide-react";

export interface EmailCodeMessages {
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
    description: string;
    codeLabel: string;
    codePlaceholder: string;
    submit: string;
    pending: string;
    useDifferentEmail: string;
    errorTitle: string;
    error: string;
  };
  backToSignIn: string;
}

export function EmailCodeFlow({
  redirectTo = "/",
  apiUrl,
  messages,
}: {
  redirectTo?: string;
  apiUrl: string;
  messages: EmailCodeMessages;
}) {
  const [email, setEmail] = useState("");
  const [stage, setStage] = useState<"request" | "consume">("request");

  return stage === "consume" ? (
    <EmailCodeConsumer
      email={email}
      redirectTo={redirectTo}
      messages={messages}
      onUseDifferentEmail={() => setStage("request")}
    />
  ) : (
    <EmailCodeRequestForm
      email={email}
      onEmailChange={setEmail}
      onSubmitted={() => setStage("consume")}
      apiUrl={apiUrl}
      messages={messages}
    />
  );
}

function EmailCodeRequestForm({
  email,
  onEmailChange,
  onSubmitted,
  apiUrl,
  messages,
}: {
  email: string;
  onEmailChange: (value: string) => void;
  onSubmitted: () => void;
  apiUrl: string;
  messages: EmailCodeMessages;
}) {
  const [pending, setPending] = useState(false);
  const [failed, setFailed] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setFailed(false);

    try {
      const sent = await requestEmailCode(email, apiUrl);
      if (!sent) {
        setFailed(true);
        return;
      }

      onSubmitted();
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
          <h1>{messages.request.title}</h1>
        </CardTitle>
        <CardDescription className="text-slate-300">
          {messages.request.description}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event) => void handleSubmit(event)}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="email-code-email">
                {messages.request.emailLabel}
              </FieldLabel>
              <Input
                id="email-code-email"
                name="email"
                type="email"
                placeholder={messages.request.emailPlaceholder}
                autoComplete="email"
                required
                value={email}
                disabled={pending}
                onChange={(event) => onEmailChange(event.currentTarget.value)}
                className="h-10 border-white/10 bg-white/5 text-white placeholder:text-slate-500"
                aria-invalid={failed}
                aria-describedby={
                  failed ? "email-code-request-error" : undefined
                }
              />
              {failed ? (
                <FieldError id="email-code-request-error" role="alert">
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
      </CardContent>
    </Card>
  );
}

function EmailCodeConsumer({
  email,
  redirectTo,
  messages,
  onUseDifferentEmail,
}: {
  email: string;
  redirectTo: string;
  messages: EmailCodeMessages;
  onUseDifferentEmail: () => void;
}) {
  const { signIn } = useAuth();
  const [code, setCode] = useState("");
  const [pending, setPending] = useState(false);
  const [failed, setFailed] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setFailed(false);

    try {
      await signIn("credentials", {
        emailCode: code,
        emailCodeEmail: email,
        redirectTo,
      });
      setPending(false);
    } catch {
      setPending(false);
      setFailed(true);
    }
  }

  return (
    <Card className="border-white/10 bg-slate-900/85 text-white shadow-2xl shadow-sky-950/30 backdrop-blur">
      <CardHeader className="text-center">
        <CardTitle className="text-2xl">
          <h1>
            {failed ? messages.consume.errorTitle : messages.consume.title}
          </h1>
        </CardTitle>
        <CardDescription className="text-slate-300" role={failed ? "alert" : undefined}>
          {failed
            ? messages.consume.error
            : `${messages.consume.description} ${email}`}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {failed ? (
          <div className="flex flex-col gap-4 text-center">
            <AlertCircle
              className="mx-auto size-5 text-amber-200"
              aria-hidden="true"
            />
            <button
              type="button"
              onClick={onUseDifferentEmail}
              className="text-sky-200 underline-offset-4 hover:underline"
            >
              {messages.consume.useDifferentEmail}
            </button>
            <Link
              href="/sign-in"
              className="text-slate-300 underline-offset-4 hover:underline"
            >
              {messages.backToSignIn}
            </Link>
          </div>
        ) : (
          <form onSubmit={(event) => void handleSubmit(event)}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="email-code-input">
                  {messages.consume.codeLabel}
                </FieldLabel>
                <Input
                  id="email-code-input"
                  name="code"
                  type="text"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  pattern="[0-9]{6}"
                  maxLength={6}
                  placeholder={messages.consume.codePlaceholder}
                  required
                  value={code}
                  disabled={pending}
                  onChange={(event) => setCode(event.currentTarget.value)}
                  className="h-12 text-center text-2xl tracking-[0.5em] border-white/10 bg-white/5 text-white placeholder:text-slate-500"
                />
              </Field>
              <Field>
                <Button
                  type="submit"
                  size="lg"
                  disabled={pending || code.trim().length !== 6}
                >
                  {pending ? (
                    <Loader2
                      className="mr-2 size-4 animate-spin"
                      aria-hidden="true"
                    />
                  ) : null}
                  {pending ? messages.consume.pending : messages.consume.submit}
                </Button>
                <FieldDescription className="text-center text-slate-300">
                  <button
                    type="button"
                    onClick={onUseDifferentEmail}
                    className="text-sky-200 underline-offset-4 hover:underline"
                  >
                    {messages.consume.useDifferentEmail}
                  </button>
                </FieldDescription>
              </Field>
            </FieldGroup>
          </form>
        )}
      </CardContent>
    </Card>
  );
}
