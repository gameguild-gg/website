import type {NextAuthConfig} from "next-auth";
import Credentials from "next-auth/providers/credentials";
import Google from "next-auth/providers/google";
import {LocalSignInResponseDto} from "@/dtos/auth/local-sign-in.response.dto";
import {environment} from "@/config/environment";


export const authConfig = {
  callbacks: {
    signIn: async ({user, account, profile, email, credentials}) => {
      if (account?.provider === "google") {

        // TODO:
        //  After signing in with Google, check if the user is in the database.
        //  If the user is not in the database, reject the sign-in.
        //  If the user is in the database, return true to allow user to sign in.

        // TODO: Sample code below:

        let url = `${environment.BACKEND_URL}/auth/google/callback/${account?.id_token}`;

        const dbUser = await fetch(
          url,
          {
            method: "GET",
            headers: {
              "Content-Type": "application/json",
            },
          },
        ).then((r) => r.json());

        const response = dbUser as LocalSignInResponseDto;

        if (!response) return false;

        user.id = response.user.id;
        user.email = response.user.email;
        // user.firstName = dbUser?.data?.user?.firstName;
        // user.lastName = dbUser?.data?.user?.lastName;
        // user.avatar = dbUser?.data?.user?.avatar;
        // user.isEmailVerified = dbUser?.data?.user?.isEmailVerified;
        // user.isPhoneVerified = dbUser?.data?.user?.isPhoneVerified;
        user.accessToken = response.accessToken;
        user.refreshToken = response.refreshToken;

        // Return true to allowing user sign-in with the Google OAuth Credential.
        return true;
      }
      return false;
    },
  },
  pages: {
    signIn: "/sign-in"
  },
  providers: [
    Credentials({
      id: "web-3",
      name: "web-3",
      credentials: {
        message: {
          label: "Message",
          type: "text",
          placeholder: "0x0",
        },
        signature: {
          label: "Signature",
          type: "text",
          placeholder: "0x0",
        },
      },
      async authorize(credentials, req) {
        const {message, signature} = credentials;

        const response = await fetch(
          `${process.env.NEXT_JS_BACKEND_URL}/auth/web3/sign-in/validate`,
          {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
            },
            body: JSON.stringify({message, signature}),
          }
        );

        const data = response.json();

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
          request_uri: environment.SignInGoogleCallbackUrl
        }
      }
    }),
  ],
  session: {
    strategy: "jwt"
  },
}satisfies NextAuthConfig;
