"use client"

import { type FormEvent, useEffect, useState } from "react"
import { Link } from "@/i18n/navigation"
import { useLocale } from "next-intl"
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
import { PasswordInput } from "@/components/ui/password-input"

export function SignInForm({
  className,
  redirectTo = "/",
  providers,
  ...props
}: React.ComponentProps<"div"> & {
  redirectTo?: string
  providers?: React.ReactNode
}) {
  const { signIn, isLoading, error, clearError } = useAuth()
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [isHydrated, setIsHydrated] = useState(false)
  const locale = useLocale()

  useEffect(() => {
    setIsHydrated(true)
  }, [])

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    clearError()
    setFieldErrors({})

    const formData = new FormData(e.currentTarget)
    const email = formData.get("email") as string
    const password = formData.get("password") as string

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
            <h1>Welcome back to GameGuild</h1>
          </CardTitle>
          <CardDescription className="text-slate-300">
            Sign in to continue learning, testing projects, and collaborating with the community.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {providers && (
            <>
              <div className="mb-6">{providers}</div>
              <div className="flex w-full items-center gap-3 text-xs text-slate-400">
                <div className="h-px flex-1 bg-white/10" />
                <span>or with email</span>
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
                  placeholder="m@example.com"
                  autoComplete="email"
                  required
                  disabled={isLoading}
                  className="border-white/10 bg-white/5 text-white placeholder:text-slate-500"
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
                <div className="flex items-center">
                  <FieldLabel htmlFor="password">Password</FieldLabel>
                  <Link
                    href="/forgot-password"
                    locale={locale}
                    className="ml-auto text-sm text-sky-200 underline-offset-4 hover:underline"
                    tabIndex={-1}
                  >
                    Forgot your password?
                  </Link>
                </div>
                <PasswordInput
                  id="password"
                  name="password"
                  autoComplete="current-password"
                  required
                  disabled={isLoading}
                  className="border-white/10 bg-white/5 text-white"
                  aria-invalid={!!fieldErrors.password}
                  onChange={() =>
                    fieldErrors.password &&
                    setFieldErrors((prev) => ({ ...prev, password: "" }))
                  }
                />
                {fieldErrors.password && (
                  <FieldError>{fieldErrors.password}</FieldError>
                )}
              </Field>
              {error && (
                <FieldError>{error.message}</FieldError>
              )}
              <Field>
                <Button type="submit" disabled={isLoading || !isHydrated}>
                  {isLoading ? "Signing in..." : "Sign in"}
                </Button>
                <FieldDescription className="text-center text-slate-300">
                  Don&apos;t have an account?{" "}
                  <Link
                    href={redirectTo ? `/sign-up?redirectTo=${encodeURIComponent(redirectTo)}` : "/sign-up"}
                    locale={locale}
                    className="text-sky-200 underline-offset-4 hover:underline"
                  >
                    Sign up
                  </Link>
                </FieldDescription>
              </Field>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
      <FieldDescription className="px-6 text-center text-slate-400">
        By clicking continue, you agree to our{" "}
        <Link href="/legal/terms-of-service" locale={locale} className="text-sky-200 underline-offset-4 hover:underline">Terms of Service</Link> and{" "}
        <Link href="/legal/privacy" locale={locale} className="text-sky-200 underline-offset-4 hover:underline">Privacy Policy</Link>.
      </FieldDescription>
    </div>
  )
}
