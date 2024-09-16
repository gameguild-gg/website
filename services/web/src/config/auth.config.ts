import {NextAuthConfig, User} from 'next-auth';
import Google from 'next-auth/providers/google';
import Credentials from 'next-auth/providers/credentials';
import {environment} from '@/config/environment';
import {authApi} from '@/lib/apinest';

export const authConfig = {
  callbacks: {
    signIn: async ({user, account, profile, email, credentials}) => {
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
        return Boolean(user.wallet && user.accessToken && user.refreshToken);
      }
      return false;
    },
    jwt: async ({token, user, trigger, session, account}) => {
      return {...token, ...user};
    },
    session: async ({session, token, user}) => {
      session.user =
        {
          id: user.id,
          email: user.email,
          name: user.name,
          image: user.image,
          wallet: user.wallet,
          emailVerified: null,
          accessToken: (token.accessToken as string) ?? null,
          refreshToken: (token.refreshToken as string) ?? null,
        }
      return session;
    },
  },
  pages: {
    signIn: '/connect',

  },
  providers: [
    // todo: implement refresh token
    // Credentials({
    //   type: 'credentials',
    //   id: 'email',
    //   name: 'email',
    //   credentials: {
    //     refreshToken: {
    //       label: 'Refresh Token',
    //       type: 'email',
    //       placeholder: '',
    //     },
    //   },
    //   async authorize(credentials): Promise<User | null> {
    //     const refreshToken: string = credentials?.refreshToken as string;
    //   },
    // }),
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
} satisfies NextAuthConfig;

declare module "next-auth" {
  interface Session {
    accessToken?: string
  }
}

declare module "next-auth" {
  interface JWT {
    accessToken?: string
  }
}

declare module "next-auth" {
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