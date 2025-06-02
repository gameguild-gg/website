'use client';

import React, { ComponentPropsWithoutRef } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { signInWithGoogle } from '@/lib/auth';
import { Link } from '@/i18n/navigation';

export const SignInForm = ({ className, ...props }: ComponentPropsWithoutRef<'div'>) => {
  return (
    <div className={cn('flex flex-col w-full max-w-sm gap-6', className)} {...props}>
      <Card>
        <CardHeader className="text-center">
          <CardTitle className="text-xl">Welcome back</CardTitle>
          <CardDescription>Sign-in with your email and password</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-6">
            <form>
              <div className="grid gap-6">
                <div className="grid gap-2">
                  <Label htmlFor="email">Email</Label>
                  <Input id="email" type="email" placeholder="email@example.com" required />
                </div>
                <div className="grid gap-2">
                  <div className="flex items-center">
                    <Label htmlFor="password">Password</Label>
                    <a href="#" className="ml-auto text-sm underline-offset-4 hover:underline">
                      Forgot your password?
                    </a>
                  </div>
                  <Input id="password" type="password" required />
                </div>
                <Button type="submit" className="w-full">
                  Sign in
                </Button>
              </div>
            </form>
            <div className="relative text-center text-sm after:absolute after:inset-0 after:top-1/2 after:z-0 after:flex after:items-center after:border-t after:border-border">
              <span className="relative z-10 bg-card rounded-full px-2 text-muted-foreground">Or continue with</span>
            </div>
            <div className="flex flex-col gap-4">
              <Button variant="outline" className="w-full" onClick={() => signInWithGoogle()}>
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" className="size-5 mr-2" aria-hidden="true">
                  <path
                    d="M12.0003 4.75C13.7703 4.75 15.3553 5.36002 16.6053 6.54998L20.0303 3.125C17.9502 1.19 15.2353 0 12.0003 0C7.31028 0 3.25527 2.69 1.28027 6.60998L5.27028 9.70498C6.21525 6.86002 8.87028 4.75 12.0003 4.75Z"
                    fill="#EA4335"
                  />
                  <path
                    d="M23.49 12.275C23.49 11.49 23.415 10.73 23.3 10H12V14.51H18.47C18.18 15.99 17.34 17.25 16.08 18.1L19.945 21.1C22.2 19.01 23.49 15.92 23.49 12.275Z"
                    fill="#4285F4"
                  />
                  <path
                    d="M5.26498 14.2949C5.02498 13.5699 4.88495 12.7999 4.88495 11.9999C4.88495 11.1999 5.01998 10.4299 5.26498 9.7049L1.27496 6.60986C0.45996 8.22986 0 10.0599 0 11.9999C0 13.9399 0.45996 15.7699 1.27996 17.3899L5.26498 14.2949Z"
                    fill="#FBBC05"
                  />
                  <path
                    d="M12.0001 24C15.2401 24 17.9651 22.935 19.9451 21.095L16.0801 18.095C15.0051 18.82 13.6201 19.245 12.0001 19.245C8.8701 19.245 6.21506 17.135 5.26506 14.29L1.27502 17.385C3.25502 21.31 7.3101 24 12.0001 24Z"
                    fill="#34A853"
                  />
                </svg>
                Sign in with Google
              </Button>
            </div>
            <div className="text-center text-sm">
              Don&apos;t have an account?{' '}
              <Link href="/sign-up" className="underline underline-offset-4">
                Sign up
              </Link>
            </div>
          </div>
        </CardContent>
      </Card>
      <div className="text-balance text-center text-xs text-muted-foreground [&_a]:underline [&_a]:underline-offset-4 [&_a]:hover:text-primary">
        By clicking continue, you agree to our <Link href="/terms-of-service">Terms of Service</Link> and <Link href="/polices/privacy">Privacy Policy</Link>.
      </div>
    </div>
  );
};
