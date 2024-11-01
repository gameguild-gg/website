'use client';
import { useEffect } from 'react';

// this page should not have sessions provider to avoid refreshing the token while invalidating it
export default function SignOut() {
  async function signOut() {
    try {
      // call backend to sign out
      const res = await fetch('/api/auth/signout', {
        method: 'GET',
      });

      // wait for the signout to complete
      await res.json();

      if (res.status !== 200) {
        console.error('Failed to sign out');
      }
    } catch (e) {
      console.error(JSON.stringify(e));
    } finally {
      // redirect to connect
      window.location.href = '/connect';
    }
  }

  useEffect(() => {
    signOut();
  }, []);

  return <div>Disconnecting...</div>;
}
