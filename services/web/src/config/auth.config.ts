import type { NextAuthConfig } from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import Google from 'next-auth/providers/google';
import { environment } from '@/config/environment';
import { authApi } from '@/lib/apinest';

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
      }
      return false;
    },
  },
  pages: {
    signIn: '/sign-in',
  },
  providers: [
    Credentials({
      type: 'credentials',
      id: 'web-3',
      name: 'web-3',
      credentials: {
        message: {
          label: 'Message',
          type: 'text',
          placeholder: '0x0',
        },
        signature: {
          label: 'Signature',
          type: 'text',
          placeholder: '0x0',
        },
      },
      async authorize(credentials) {
        const message: string = credentials?.message as string;
        const signature: string = credentials.signature as string;

        const response =
          await authApi.authControllerValidateWeb3SignInChallenge({
            message,
            signature,
            address: '',
          });

        console.log(response);
        // TODO: send the signature to the server to verify the user's identity.
        // It should be done using the auth.js (next-auth) library.
        //   //       // TODO: Verify the signature on the server.
        // const validationResponse = await fetch('api/(auth)/web3/sign-in', {
        //   method: 'POST',
        //   headers: {
        //     'Content-Type': 'application/json',
        //   },
        //   body: JSON.stringify({
        //     accountAddress: accountAddress,
        //     message: message,
        //     signature: signature,
        //   }),
        // });
        return null;
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
