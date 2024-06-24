import type { NextAuthConfig, User } from 'next-auth';
import Google from 'next-auth/providers/google';
import Credentials from 'next-auth/providers/credentials';
import { environment } from '@/config/environment';
import { authApi } from '@/lib/apinest';
import { sendVerificationRequest } from '@/lib/auth/authSendRequest';

export const authConfig = {
  callbacks: {
    signIn: async ({ user, account, profile, email, credentials }) => {
      if (account?.provider === 'google') {
        // TODO:
        //  After signing in with Google, check if the user is in the database.
        //  If the user is not in the database, reject the sign-in.
        //  If the user is in the database, return true to allow user to sign in.

        // TODO: Sample code below:

        if (!account?.id_token) return false;
        const response = await authApi.authControllerSignInWithGoogle(
          account?.id_token,
        );

        if (!response || response.status !== 200) return false;

        user.id = response.data.user.id;
        user.email = response.data.user.email;
        // user.firstName = dbUser?.data?.user?.firstName;
        // user.lastName = dbUser?.data?.user?.lastName;
        // user.avatar = dbUser?.data?.user?.avatar;
        // user.isEmailVerified = dbUser?.data?.user?.isEmailVerified;
        // user.isPhoneVerified = dbUser?.data?.user?.isPhoneVerified;
        user.accessToken = response.data.accessToken;
        user.refreshToken = response.data.refreshToken;

        // Return true to allowing user sign-in with the Google OAuth Credential.
        return true;
      } else if (account?.provider === 'web-3') {
        return !!user.wallet;
      }
      return false;
    },
  },
  pages: {
    signIn: '/sign-in',
  },
  providers: [
    {
      id: 'http-email',
      name: 'Email',
      type: 'email',
      // maxAge: 60 * 60 * 24, // Email link will expire in 24 hours
      sendVerificationRequest: sendVerificationRequest,
    },
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

        const response =
          await authApi.authControllerValidateWeb3SignInChallenge({
            signature,
            address,
          });

        if (!response || response.status < 200 || response.status > 299)
          return null;

        const accessToken = response.data.accessToken;
        const refreshToken = response.data.refreshToken;
        const user = response.data.user;

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
};
