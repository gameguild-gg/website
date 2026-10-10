"use client"

import { type FormEvent, useState, useSyncExternalStore } from "react"
import { Link } from "@/i18n/navigation"
import { useAuth } from "@game-guild/client/react"
import { cn } from "@/lib/utils"
import { Button } from "@game-guild/ui/components/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@game-guild/ui/components/card"
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@game-guild/ui/components/field"
import { Input } from "@game-guild/ui/components/input"
import { Checkbox } from "@game-guild/ui/components/checkbox"
import { PasswordInput } from "@/components/ui/password-input"

const subscribeToHydration = () => () => undefined

export function SignInForm({
  className,
  redirectTo = "/",
  magicLinkLabel,
  emailCodeLabel,
  providers,
  ...props
}: React.ComponentProps<"div"> & {
  redirectTo?: string
  magicLinkLabel?: string
  emailCodeLabel?: string
  providers?: React.ReactNode
}) {
  const { signIn, isLoading, error, clearError } = useAuth()
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const isHydrated = useSyncExternalStore(subscribeToHydration, () => true, () => false)

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    clearError()
    setFieldErrors({})

    const formData = new FormData(e.currentTarget)
    const email = formData.get("email") as string
    const password = formData.get("password") as string
    // Remember-me opt-in: persistent backend refresh lifetime + durable cookie storage.
    const rememberMe = formData.get("rememberMe") === "on"

    if (!email) {
      setFieldErrors((prev) => ({ ...prev, email: "Email is required." }))
      return
    }
    if (!password) {
      setFieldErrors((prev) => ({ ...prev, password: "Password is required." }))
      return
    }

    try {
      await signIn("credentials", {
        email,
        password,
        rememberMe,
        redirectTo,
      })
    } catch {
      // error state is set by useAuth
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
          {providers && (
            <>
              <div className="mb-6">{providers}</div>
              <div className="flex w-full items-center gap-3 text-xs text-slate-400">
                <div className="h-px flex-1 bg-white/10" />
                <span>or sign in with email</span>
                <div className="h-px flex-1 bg-white/10" />
              </div>
            </>
          )}
          <form
            method="post"
            onSubmit={handleSubmit}
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
              {error && (
                <FieldError>{error.message}</FieldError>
              )}
              <Field>
                <Button type="submit" size="lg" disabled={isLoading || !isHydrated}>
                  {isLoading ? "Signing in..." : "Sign in"}
                </Button>
                <FieldDescription className="text-center text-slate-300">
                  Don&apos;t have an account?{" "}
                  <Link
                    href={redirectTo ? `/sign-up?redirectTo=${encodeURIComponent(redirectTo)}` : "/sign-up"}
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
        </CardContent>
      </Card>
    </div>
  )
}
