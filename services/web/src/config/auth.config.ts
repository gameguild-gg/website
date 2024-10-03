import type { NextAuthConfig, User } from 'next-auth';
import Google from 'next-auth/providers/google';
import Credentials from 'next-auth/providers/credentials';
import { environment } from '@/config/environment';
import { Api, AuthApi } from '@game-guild/apiclient';
import { signOut } from 'next-auth/react';
import type { Provider } from 'next-auth/providers';
import { NextResponse } from 'next/server';

// list of providers
const providers: Provider[] = [];

if (
  !environment.GoogleClientId ||
  !environment.GoogleClientSecret ||
  !environment.SignInGoogleCallbackUrl
) {
  throw new Error(
    'GoogleClientId, GoogleClientSecret, and SignInGoogleCallbackUrl must be set in the environment. Please check your .env file. Or talk with us to provide them to you.',
  );
}
providers.push(
  Google({
    clientId: environment.GoogleClientId,
    clientSecret: environment.GoogleClientSecret,
    authorization: {
      params: {
        request_uri: environment.SignInGoogleCallbackUrl,
      },
    },
  }),
);

export const authConfig = {
  trustHost: true, // todo: fix [auth][error] UntrustedHost: Host must be trusted. URL was: https://localhost:3000/api/auth/session. Read more at https://errors.authjs.dev#untrustedhost
  debug: true,
  callbacks: {
    signIn: async ({ user, account, profile, email, credentials }) => {
      if (account?.provider === 'google') {
        const api = new AuthApi({
          basePath: process.env.NEXT_PUBLIC_API_URL,
        });

        // TODO:
        //  After signing in with Google, check if the user is in the database.
        //  If the user is not in the database, reject the sign-in.
        //  If the user is in the database, return true to allow user to sign in.

        // TODO: Sample code below:

        if (!account?.id_token) return false;

        const response = await api.authControllerSignInWithGoogle(
          account?.id_token,
        );
        if (response.status >= 400) {
          console.error(response.body);
          return false;
        }

        user.id = response.body.user.id;
        user.email = response.body.user.email;
        user.accessToken = response.body.accessToken;
        user.refreshToken = response.body.refreshToken;

        return true;
      } else if (account?.provider === 'web-3') {
        return Boolean(user.wallet && user.accessToken && user.refreshToken);
      } else if (account?.provider === 'magic-link') {
        return Boolean(user.accessToken && user.refreshToken);
      }
      return false;
    },
    jwt: async ({ token, user, trigger, session, account }) => {
      // merge token
      const merged = { ...token, ...user };

      // Check if the token is expired
      const isExpired = token.exp && Date.now() >= token.exp * 1000;
      if (isExpired) {
        // Delete the session
        await signOut({ redirect: false });
        return {};
      }

      return token;
    },
    session: async ({ session, token, user }) => {
      session = {
        ...session,
        user: { ...session.user, ...user },
        ...token,
      };

      // Check if the token is expired
      const isExpired = token.exp && Date.now() >= token.exp * 1000;
      if (isExpired) {
        // Redirect to the /connect page
        const response = NextResponse.redirect('/connect');
        response.cookies.delete('next-auth.session-token');
        return response;
      }
      return session;
    },
  },
  pages: {
    signIn: '/connect',
    error: '/connect',
    // verifyRequest: '/api/auth/verify-request',
    // signOut: '/api/auth/signout',
  },
  providers: [
    ...providers,
    Credentials({
      type: 'credentials',
      id: 'magic-link',
      name: 'magic-link',
      credentials: {
        token: {
          label: 'Token',
          type: 'text',
          placeholder: '0x0',
        },
      },
      async authorize(credentials): Promise<User | null> {
        const token: string = credentials?.token as string;

        const api = new AuthApi({
          basePath: process.env.NEXT_PUBLIC_API_URL,
        });

        const response = await api.authControllerRefreshToken({
          headers: { Authorization: `Bearer ${token}` },
        });

        if (response.status >= 400) {
          console.error(JSON.stringify(response.body));
          return null;
        }
        const body = response.body as Api.LocalSignInResponseDto;

        const accessToken = body.accessToken;
        const refreshToken = body.refreshToken;
        const userResponse = await api.authControllerGetCurrentUser({
          headers: { Authorization: `Bearer ${body.accessToken}` },
        });

        if (userResponse.status >= 400) {
          console.error(JSON.stringify(userResponse.body));
          return null;
        }

        const user = userResponse.body as Api.UserEntity;

        return {
          id: user.id,
          email: user.email,
          name: user.username,
          image: user.profile?.picture,
          wallet: user.walletAddress,
          accessToken,
          refreshToken,
        };
      },
    }),
    Credentials({
      type: 'credentials',
      id: 'web-3',
      name: 'web-3',
      credentials: {
        signature: {
          label: 'Signature',
          type: 'text',
          placeholder: '0x0',
        },
        address: {
          label: 'address',
          type: 'text',
          placeholder: '0x0',
        },
      },
      async authorize(credentials): Promise<User | null> {
        const signature: string = credentials?.signature as string;
        const address: string = credentials?.address as string;

        const api = new AuthApi({
          basePath: process.env.NEXT_PUBLIC_API_URL,
        });

        const response = await api.authControllerValidateWeb3SignInChallenge({
          signature,
          address,
        });

        if (response.status >= 400) {
          console.error(JSON.stringify(response.body));
          return null;
        }

        const body = response.body as Api.LocalSignInResponseDto;

        const accessToken = body.accessToken;
        const refreshToken = body.refreshToken;
        const user = body.user;

        return {
          id: user.id,
          email: user.email,
          name: user.username,
          image: user.profile.picture,
          wallet: user.walletAddress,
          accessToken,
          refreshToken,
        };
      },
    }),
  ],
  session: {
    strategy: 'jwt',
  },
} satisfies NextAuthConfig;

declare module 'next-auth' {
  interface Session {
    accessToken?: string;
  }
}

declare module 'next-auth' {
  interface JWT {
    accessToken?: string;
  }
}

declare module 'next-auth' {
  interface User {
    id?: string;
    name?: string | null;
    email?: string | null;
    image?: string | null;
    wallet?: string | null;
    accessToken?: string | null;
    refreshToken?: string | null;
  }
}
