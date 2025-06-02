import Google from 'next-auth/providers/google';
import { environment } from '@/configs/environment';
import { NextAuthConfig } from 'next-auth';

export const authConfig: NextAuthConfig = {
  providers: [
    Google({
      clientId: environment.googleClientId,
      clientSecret: environment.googleClientSecret,
      authorization: {
        params: {
          request_uri: environment.signInGoogleCallbackUrl,
        },
      },
    }),
  ],
  session: { strategy: 'jwt' },
  callbacks: {
    async signIn({ user, account, profile, email, credentials }) {
      if (account?.provider !== 'google') {
      } else {
        // TODO:
        //  After signing in with Google, check if the user is in the database.
        //  If the user is not in the database, reject the sign-in.
        //  If the user is in the database, return true to allow user to sign in.
        if (!account?.id_token) return false;
        // Example of sending the token to your backend:
        // const response = await fetch(`${environment.apiBaseUrl}/auth/google`, {
        //   method: 'POST',
        //   headers: { 'Content-Type': 'application/json' },
        //   body: JSON.stringify({ id_token: account.id_token }),
        // });
        //
        // if (!response.ok) return false;
        // const data = await response.json();
        // user.accessToken = data.accessToken;
        // user.refreshToken = data.refreshToken;

        // For testing:
        (user as any).accessToken = 'TESTE';
        (user as any).refreshToken = 'TESTE';
      }
      return true;
    },
    async jwt({ token, user, account, profile, trigger, session }) {
      if (user) {
        token.id = user.id;

        token.accessToken = (user as any).accessToken;
        token.refreshToken = (user as any).refreshToken;
      }
      console.log('jwt', token);

      // check if the token is expired.

      // try {
      // const response = await fetch(`${environment.apiBaseUrl}/auth/refresh`, {
      //   method: 'POST',
      //   headers: {
      //     'Content-Type': 'application/json',
      //     Authorization: `Bearer ${token.refreshToken}`,
      //   },
      //   body: JSON.stringify({ refreshToken: token.refreshToken }),
      // });

      // if (!response.ok) throw new Error('Failed to refresh token');

      // const data = await response.json();

      // token.accessToken = data.accessToken;
      // token.refreshToken = data.refreshToken;
      // } catch (error) {
      //   // console.error('Error refreshing token:', error);
      //   // Handle token refresh error (e.g., redirect to sign-in page)
      //   // token.error = 'RefreshTokenError';
      // }
      return token;
    },
    async session({ session, token }) {
      session.accessToken = token.accessToken as string | undefined;
      return session;
    },
  },
};

declare module 'next-auth' {
  interface JWT {
    id: string;

    accessToken?: string;
    refreshToken?: string;
  }
}

declare module 'next-auth' {
  interface Session {
    accessToken?: string;
  }
}
