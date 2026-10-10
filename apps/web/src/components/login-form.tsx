'use client';

import { type FormEvent, useState } from 'react';
import Link from 'next/link';
import { useAuth } from '@game-guild/client/react';
import { cn } from '@/lib/utils';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel, FieldSeparator } from '@game-guild/ui/components/field';
import { Input } from '@game-guild/ui/components/input';
import { Checkbox } from '@game-guild/ui/components/checkbox';

export function SignInForm({ className, ...props }: React.ComponentProps<'div'>) {
  const { signIn, isLoading, error, clearError } = useAuth();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    clearError();
    setFieldErrors({});

    const formData = new FormData(e.currentTarget);
    const email = formData.get('email') as string;
    const password = formData.get('password') as string;
    // Remember-me opt-in: persistent backend refresh lifetime + durable cookie storage.
    const rememberMe = formData.get('rememberMe') === 'on';

    if (!email) {
      setFieldErrors((prev) => ({ ...prev, email: 'Email is required.' }));
      return;
    }
    if (!password) {
      setFieldErrors((prev) => ({ ...prev, password: 'Password is required.' }));
      return;
    }

    try {
      await signIn('credentials', {
        email,
        password,
        rememberMe,
        redirectTo: '/',
      });
    } catch {
      // error state is set by useAuth
    }
  }

  async function handleOAuthSignIn(provider: string) {
    clearError();
    try {
      await signIn(provider, { redirectTo: '/' });
    } catch {
      // error state is set by useAuth
    }
  }

  return (
    <div className={cn('flex flex-col gap-6', className)} {...props}>
      <Card>
        <CardHeader className="text-center">
          <CardTitle className="text-xl">Welcome back</CardTitle>
          <CardDescription>Login with your Apple or Google account</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} noValidate>
            <FieldGroup>
              <Field>
                <Button variant="outline" type="button" disabled={isLoading} onClick={() => handleOAuthSignIn('apple')}>
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
                    <path
                      d="M12.152 6.896c-.948 0-2.415-1.078-3.96-1.04-2.04.027-3.91 1.183-4.961 3.014-2.117 3.675-.546 9.103 1.519 12.09 1.013 1.454 2.208 3.09 3.792 3.039 1.52-.065 2.09-.987 3.935-.987 1.831 0 2.35.987 3.96.948 1.637-.026 2.676-1.48 3.676-2.948 1.156-1.688 1.636-3.325 1.662-3.415-.039-.013-3.182-1.221-3.22-4.857-.026-3.04 2.48-4.494 2.597-4.559-1.429-2.09-3.623-2.324-4.39-2.376-2-.156-3.675 1.09-4.61 1.09zM15.53 3.83c.843-1.012 1.4-2.427 1.245-3.83-1.207.052-2.662.805-3.532 1.818-.78.896-1.454 2.338-1.273 3.714 1.338.104 2.715-.688 3.559-1.701"
                      fill="currentColor"
                    />
                  </svg>
                  Login with Apple
                </Button>
                <Button variant="outline" type="button" disabled={isLoading} onClick={() => handleOAuthSignIn('google')}>
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
                    <path
                      d="M12.48 10.92v3.28h7.84c-.24 1.84-.853 3.187-1.787 4.133-1.147 1.147-2.933 2.4-6.053 2.4-4.827 0-8.6-3.893-8.6-8.72s3.773-8.72 8.6-8.72c2.6 0 4.507 1.027 5.907 2.347l2.307-2.307C18.747 1.44 16.133 0 12.48 0 5.867 0 .307 5.387.307 12s5.56 12 12.173 12c3.573 0 6.267-1.173 8.373-3.36 2.16-2.16 2.84-5.213 2.84-7.667 0-.76-.053-1.467-.173-2.053H12.48z"
                      fill="currentColor"
                    />
                  </svg>
                  Login with Google
                </Button>
              </Field>
              <FieldSeparator className="*:data-[slot=field-separator-content]:bg-card">Or continue with</FieldSeparator>
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
                  aria-invalid={!!fieldErrors.email}
                  onChange={() => fieldErrors.email && setFieldErrors((prev) => ({ ...prev, email: '' }))}
                />
                {fieldErrors.email && <FieldError>{fieldErrors.email}</FieldError>}
              </Field>
              <Field>
                <div className="flex items-center">
                  <FieldLabel htmlFor="password">Password</FieldLabel>
                  <Link href="/forgot-password" className="ml-auto text-sm underline-offset-4 hover:underline" tabIndex={-1}>
                    Forgot your password?
                  </Link>
                </div>
                <Input
                  id="password"
                  name="password"
                  type="password"
                  autoComplete="current-password"
                  required
                  disabled={isLoading}
                  aria-invalid={!!fieldErrors.password}
                  onChange={() => fieldErrors.password && setFieldErrors((prev) => ({ ...prev, password: '' }))}
                />
                {fieldErrors.password && <FieldError>{fieldErrors.password}</FieldError>}
              </Field>
              <Field>
                <label
                  htmlFor="rememberMe"
                  className="flex cursor-pointer items-center gap-2 text-sm select-none"
                >
                  <Checkbox
                    id="rememberMe"
                    name="rememberMe"
                    disabled={isLoading}
                    aria-label="Remember me on this device"
                  />
                  Remember me
                </label>
                <FieldDescription className="text-center">
                  Keeps you signed in on this device for up to 30 days.
                </FieldDescription>
              </Field>
              {error && <FieldError>{error.message}</FieldError>}
              <Field>
                <Button type="submit" disabled={isLoading}>
                  {isLoading ? 'Signing in...' : 'Login'}
                </Button>
                <FieldDescription className="text-center">
                  Don&apos;t have an account? <Link href="/sign-up">Sign up</Link>
                </FieldDescription>
              </Field>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
