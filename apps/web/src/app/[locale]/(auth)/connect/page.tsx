'use client';

import React, { useEffect } from 'react';
import ConnectForm from '@/components/auth/connect-form';
import Cookies from 'js-cookie';
import { useSearchParams } from 'next/navigation';

export default function SignIn() {
  const searchParams = useSearchParams();
  const cacheBuster = searchParams.get('cb');

  // If we have a cache buster parameter, it means we're coming from the disconnect page
  // Let's make sure we clear any remaining session data
  useEffect(() => {
    if (cacheBuster) {
      // Clear all auth-related cookies
      const cookiesToClear = [
        'authjs.session-token',
        'authjs.csrf-token',
        'authjs.callback-url',
        'authjs.refresh-token',
        'authjs.pkce.code_verifier',
        'authjs.pkce.state',
        'next-auth.session-token',
        'next-auth.csrf-token',
        'next-auth.callback-url',
        '__Secure-next-auth.session-token',
        '__Secure-next-auth.callback-url',
        '__Secure-next-auth.csrf-token',
        '__Host-next-auth.csrf-token',
        'session',
        'token',
        'user'
      ];
      
      cookiesToClear.forEach(cookieName => {
        Cookies.remove(cookieName);
        Cookies.remove(cookieName, { path: '/' });
        document.cookie = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/;`;
      });
      
      // Clear localStorage and sessionStorage
      localStorage.clear();
      sessionStorage.clear();
      
      // Remove the cache buster parameter from the URL
      window.history.replaceState({}, document.title, '/connect');
    }
  }, [cacheBuster]);

  return <ConnectForm />;
}
