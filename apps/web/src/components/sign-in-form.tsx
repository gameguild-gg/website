"use client";

import { type FormEvent, useState, useSyncExternalStore } from "react";
import { Link } from "@/i18n/navigation";
import { useAuth, type MfaEnrollmentData } from "@game-guild/client/react";
import { cn } from "@/lib/utils";
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
import { Checkbox } from "@game-guild/ui/components/checkbox";
import { PasswordInput } from "@/components/ui/password-input";

const subscribeToHydration = () => () => undefined;

export function SignInForm({
  className,
  redirectTo = "/",
  magicLinkLabel,
  emailCodeLabel,
  providers,
  ...props
}: React.ComponentProps<"div"> & {
  redirectTo?: string;
  magicLinkLabel?: string;
  emailCodeLabel?: string;
  providers?: React.ReactNode;
}) {
  const {
    signIn,
    isLoading,
    error,
    clearError,
    mfaChallenge,
    startMfaEnrollment,
    mfaEnrollmentBackupCodes,
    clearMfa,
  } = useAuth();
  const [enrollment, setEnrollment] = useState<MfaEnrollmentData | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const isHydrated = useSyncExternalStore(
    subscribeToHydration,
    () => true,
    () => false,
  );

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    clearError();
    setFieldErrors({});

    const form = e.currentTarget;
    const formData = new FormData(form);
    const email = formData.get("email") as string;
    const password = formData.get("password") as string;
    const rememberMe = formData.get("rememberMe") === "on";

    if (!email) {
      setFieldErrors((prev) => ({ ...prev, email: "Email is required." }));
      return;
    }
    if (!password) {
      setFieldErrors((prev) => ({
        ...prev,
        password: "Password is required.",
      }));
      return;
    }

    try {
      await signIn("credentials", {
        email,
        password,
        rememberMe,
        redirectTo,
      });
    } catch (err) {
      if (err instanceof Error && "type" in err && err.type === "MfaRequired")
        form.reset();
      // error state is set by useAuth
    }
  }

  async function handleMfaSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!mfaChallenge) return;
    const form = e.currentTarget;
    const data = new FormData(form);
    clearError();
    try {
      await signIn("credentials", {
        mfaToken: mfaChallenge.mfaToken,
        method: enrollment ? "Totp" : data.get("method") || "Totp",
        code: data.get("code") as string,
        redirectTo,
      });
      setEnrollment(null);
      form.reset();
    } catch {
      /* The hook keeps the current challenge and exposes the error. */
    }
  }

  async function handleMfaEnrollment() {
    try {
      setEnrollment(await startMfaEnrollment());
    } catch {
      /* Error shown below. */
    }
  }

  return (
    <div className={cn("flex flex-col gap-6", className)} {...props}>
      <Card className="border-white/10 bg-slate-900/85 text-white shadow-2xl shadow-sky-950/30 backdrop-blur">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">
            <h1>Welcome back</h1>
          </CardTitle>
          <CardDescription className="text-slate-300">
            Sign in to continue.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {providers && !mfaChallenge && !mfaEnrollmentBackupCodes?.length && (
            <>
              <div className="mb-6">{providers}</div>
              <div className="flex w-full items-center gap-3 text-xs text-slate-400">
                <div className="h-px flex-1 bg-white/10" />
                <span>or sign in with email</span>
                <div className="h-px flex-1 bg-white/10" />
              </div>
            </>
          )}
          {mfaEnrollmentBackupCodes?.length ? (
            <FieldGroup>
              <FieldDescription>
                Save these recovery codes securely. They are shown once and each
                code can be used once.
              </FieldDescription>
              <ul aria-label="Recovery codes" className="space-y-1 font-mono">
                {mfaEnrollmentBackupCodes.map((code) => (
                  <li key={code}>{code}</li>
                ))}
              </ul>
              <Button
                type="button"
                onClick={() => {
                  clearMfa();
                  setEnrollment(null);
                  window.location.href = redirectTo;
                }}
              >
                I saved my codes — continue
              </Button>
            </FieldGroup>
          ) : mfaChallenge ? (
            <form
              method="post"
              onSubmit={(event) => {
                void handleMfaSubmit(event);
              }}
              data-auth-ready={isHydrated ? "true" : "false"}
            >
              <FieldGroup>
                <FieldDescription>
                  Confirm your sign-in with your authenticator app. This sign-in
                  challenge expires after five minutes.
                </FieldDescription>
                {enrollment ? (
                  <Field>
                    <FieldDescription>
                      Add this key to your authenticator app, then enter its
                      six-digit code.
                    </FieldDescription>
                    <code className="break-all rounded border border-white/10 p-3">
                      {enrollment.secretKey}
                    </code>
                    <FieldDescription>
                      Setup expires at{" "}
                      {new Date(enrollment.expiresAt).toLocaleTimeString()}.
                    </FieldDescription>
                  </Field>
                ) : (
                  <Button
                    type="button"
                    variant="outline"
                    disabled={isLoading}
                    onClick={() => {
                      void handleMfaEnrollment();
                    }}
                  >
                    Set up authenticator
                  </Button>
                )}
                {!enrollment &&
                  mfaChallenge.availableMethods.includes("BackupCode") && (
                    <Field>
                      <FieldLabel htmlFor="mfa-method">
                        Verification method
                      </FieldLabel>
                      <select
                        id="mfa-method"
                        name="method"
                        defaultValue="Totp"
                        disabled={isLoading}
                        className="h-10 rounded border border-white/10 bg-slate-900 px-3"
                      >
                        <option value="Totp">Authenticator app</option>
                        <option value="BackupCode">Recovery code</option>
                      </select>
                    </Field>
                  )}
                <Field>
                  <FieldLabel htmlFor="mfa-code">
                    Authentication code
                  </FieldLabel>
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
                  Verify and sign in
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  disabled={isLoading}
                  onClick={() => {
                    clearMfa();
                    setEnrollment(null);
                  }}
                >
                  Start sign-in again
                </Button>
              </FieldGroup>
            </form>
          ) : (
            <form
              method="post"
              onSubmit={(event) => {
                void handleSubmit(event);
              }}
              noValidate
              data-auth-ready={isHydrated ? "true" : "false"}
            >
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="email">Email</FieldLabel>
                  <Input
                    id="email"
                    name="email"
                    type="email"
                    placeholder="name@example.com"
                    autoComplete="email"
                    required
                    disabled={isLoading}
                    className="h-10 border-white/10 bg-white/5 text-white placeholder:text-slate-500"
                    aria-invalid={!!fieldErrors.email}
                    onChange={() =>
                      fieldErrors.email &&
                      setFieldErrors((prev) => ({ ...prev, email: "" }))
                    }
                  />
                  {fieldErrors.email && (
                    <FieldError>{fieldErrors.email}</FieldError>
                  )}
                </Field>
                <Field>
                  <FieldLabel htmlFor="password">Password</FieldLabel>
                  <PasswordInput
                    id="password"
                    name="password"
                    autoComplete="current-password"
                    required
                    disabled={isLoading}
                    className="h-10 border-white/10 bg-white/5 text-white"
                    aria-invalid={!!fieldErrors.password}
                    onChange={() =>
                      fieldErrors.password &&
                      setFieldErrors((prev) => ({ ...prev, password: "" }))
                    }
                  />
                  {fieldErrors.password && (
                    <FieldError>{fieldErrors.password}</FieldError>
                  )}
                  <Link
                    href="/forgot-password"
                    className="ml-auto w-fit text-sm text-sky-200 underline-offset-4 hover:underline"
                    tabIndex={-1}
                  >
                    Forgot your password?
                  </Link>
                </Field>
                <Field>
                  <label
                    htmlFor="rememberMe"
                    className="flex cursor-pointer items-center gap-2 text-sm text-slate-300 select-none"
                  >
                    <Checkbox
                      id="rememberMe"
                      name="rememberMe"
                      disabled={isLoading}
                      aria-label="Remember me on this device"
                    />
                    Remember me
                  </label>
                  <FieldDescription className="text-slate-400">
                    Keeps you signed in on this device for up to 30 days.
                  </FieldDescription>
                </Field>
                {error && <FieldError>{error.message}</FieldError>}
                <Field>
                  <Button
                    type="submit"
                    size="lg"
                    disabled={isLoading || !isHydrated}
                  >
                    {isLoading ? "Signing in..." : "Sign in"}
                  </Button>
                  <FieldDescription className="text-center text-slate-300">
                    Don&apos;t have an account?{" "}
                    <Link
                      href={
                        redirectTo
                          ? `/sign-up?redirectTo=${encodeURIComponent(redirectTo)}`
                          : "/sign-up"
                      }
                      className="text-sky-200 underline-offset-4 hover:underline"
                    >
                      Sign up
                    </Link>
                  </FieldDescription>
                  {magicLinkLabel ? (
                    <FieldDescription className="text-center text-slate-300">
                      <Link
                        href="/magic-link"
                        className="text-sky-200 underline-offset-4 hover:underline"
                      >
                        {magicLinkLabel}
                      </Link>
                    </FieldDescription>
                  ) : null}
                  {emailCodeLabel ? (
                    <FieldDescription className="text-center text-slate-300">
                      <Link
                        href="/email-code"
                        className="text-sky-200 underline-offset-4 hover:underline"
                      >
                        {emailCodeLabel}
                      </Link>
                    </FieldDescription>
                  ) : null}
                </Field>
              </FieldGroup>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
