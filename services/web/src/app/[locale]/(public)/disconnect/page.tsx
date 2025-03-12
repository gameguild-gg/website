'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Cookies from 'js-cookie';
import { signOut } from '@/auth';
import { getSession } from 'next-auth/react';

// this page should not have sessions provider to avoid refreshing the token while invalidating it
export default function SignOut() {
  const router = useRouter();
  const [isSigningOut, setIsSigningOut] = useState(true);

  async function handleSignOut() {
    try {
      setIsSigningOut(true);
      
      // Check if the user is already signed out
      const session = await getSession();
      if (!session) {
        console.log('User is already signed out, redirecting to home');
        window.location.href = '/';
        return;
      }
      
      // Step 1: Use the Next.js Auth.js signOut function
      try {
        await signOut({ redirect: false });
        console.log('Server-side sign out completed');
      } catch (e) {
        console.error('Server-side sign out failed:', e);
        // Continue with client-side logout even if server-side fails
      }
      
      // Step 2: Clear all auth-related cookies on the client side
      const cookiesToClear = [
        // Auth.js cookies
        'authjs.session-token',
        'authjs.csrf-token',
        'authjs.callback-url',
        'authjs.refresh-token',
        'authjs.pkce.code_verifier',
        'authjs.pkce.state',
        // Next-auth cookies
        'next-auth.session-token',
        'next-auth.csrf-token',
        'next-auth.callback-url',
        '__Secure-next-auth.session-token',
        '__Secure-next-auth.callback-url',
        '__Secure-next-auth.csrf-token',
        '__Host-next-auth.csrf-token',
        // Additional cookies that might be used
        'session',
        'token',
        'user'
      ];
      
      // Clear cookies with different paths and domains to be thorough
      cookiesToClear.forEach(cookieName => {
        // Clear with different path options to ensure all cookies are removed
        Cookies.remove(cookieName);
        Cookies.remove(cookieName, { path: '/' });
        Cookies.remove(cookieName, { path: '', domain: '' });
        
        // Also try to clear using document.cookie for cookies that might not be accessible via js-cookie
        document.cookie = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/;`;
        document.cookie = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; domain=${window.location.hostname};`;
        document.cookie = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; domain=.${window.location.hostname};`;
      });
      
      // Step 3: Clear localStorage items that might contain auth data
      localStorage.clear();
      
      // Step 4: Clear sessionStorage items that might contain auth data
      sessionStorage.clear();
      
      console.log('Signed out successfully');
      
      // Step 5: Force a hard navigation to the root URL with cache busting
      // This ensures the app state is completely reset
      const cacheBuster = new Date().getTime();
      window.location.href = `/?cb=${cacheBuster}`;
    } catch (e) {
      console.error('Sign out error:', JSON.stringify(e));
      alert('Failed to sign out');
      setIsSigningOut(false);
    }
  }

  useEffect(() => {
    handleSignOut();
  }, []);

  return (
    <div className="flex flex-col items-center justify-center min-h-screen">
      <div className="text-center">
        <h1 className="text-xl font-semibold mb-4">Disconnecting...</h1>
        {isSigningOut && (
          <div className="animate-pulse">Please wait while we sign you out...</div>
        )}
      </div>
    </div>
  );
}
