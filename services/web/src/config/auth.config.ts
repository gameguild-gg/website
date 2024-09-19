import type { NextAuthConfig, User } from 'next-auth';
import Google from 'next-auth/providers/google';
import Credentials from 'next-auth/providers/credentials';
import { environment } from '@/config/environment';
import { Api, AuthApi } from '@game-guild/apiclient';

export const authConfig = {
  trustHost: true, // todo: fix [auth][error] UntrustedHost: Host must be trusted. URL was: https://localhost:3000/api/auth/session. Read more at https://errors.authjs.dev#untrustedhost
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

        let response: Api.LocalSignInResponseDto;
        try {
          response = await api.authControllerSignInWithGoogle(
            account?.id_token,
          );
        } catch (e) {
          return false;
        }

        user.id = response.user.id;
        user.email = response.user.email;
        user.accessToken = response.accessToken;
        user.refreshToken = response.refreshToken;

        return true;
      } else if (account?.provider === 'web-3') {
        return Boolean(user.wallet && user.accessToken && user.refreshToken);
      } else if (account?.provider === 'magic-link') {
        return Boolean(user.accessToken && user.refreshToken);
      }
      return false;
    },
    jwt: async ({ token, user, trigger, session, account }) => {
      return { ...token, ...user };
    },
    session: async ({ session, token, user }) => {
      session = {
        ...session,
        user: { ...session.user, ...user },
        ...token,
      };
      return session;
    },
  },
  pages: {
    signIn: '/connect',
  },
  providers: [
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

        let response: Api.LocalSignInResponseDto;
        try {
          response = await api.authControllerRefreshToken({
            headers: { Authorization: `Bearer ${token}` },
          });
        } catch (e) {
          return null;
        }

        try {
          const accessToken = response.accessToken;
          const refreshToken = response.refreshToken;
          const user = await api.authControllerGetCurrentUser({
            headers: { Authorization: `Bearer ${response.accessToken}` },
          });

          return {
            id: user.id,
            email: user.email,
            name: user.username,
            image: user.profile?.picture,
            wallet: user.walletAddress,
            accessToken,
            refreshToken,
          };
        } catch (e) {
          console.error(JSON.stringify(e));
          return null;
        }
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

        let response: Api.LocalSignInResponseDto;
        try {
          response = await api.authControllerValidateWeb3SignInChallenge({
            signature,
            address,
          });
        } catch (e) {
          return null;
        }

        const accessToken = response.accessToken;
        const refreshToken = response.refreshToken;
        const user = response.user;

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
    Google({
      clientId: environment.GoogleClientId,
      clientSecret: environment.GoogleClientSecret,
      authorization: {
        params: {
          request_uri: environment.SignInGoogleCallbackUrl,
        },
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
