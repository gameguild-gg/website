'use client';

import { signIn } from 'next-auth/react';
import { useState } from 'react';

export default function SignIn() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  return (
    <>
      <div className="sm:mx-auto sm:w-full sm:max-w-sm">
        <form action="#" method="POST">
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="email"
            onChange={(e) => setEmail(e.target.value)}
            required
          />

          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          <button
            type="submit"
            disabled={!email || !password}
            onClick={() =>
              signIn('credentials', {
                email,
                password,
                redirect: true,
                callbackUrl: '/',
              })
            }
          />
        </form>
      </div>
    </>
  );
}
