import NextAuth, {type NextAuthConfig, User} from "next-auth";
import Google from "next-auth/providers/google";
import {environment} from "@/lib/environment";
import {LocalSignInResponseDto} from "@/dtos/auth/local-sign-in.response.dto";


export const authConfig = {
  pages: {
    signIn: "/sign-in"
  },
  providers: [
    Google({
      clientId: environment.GoogleClientId,
      clientSecret: environment.GoogleClientSecret,
      authorization: {
        params: {
          request_uri: `${process.env.NEXT_JS_BACKEND_URL}/api/auth/callback/google`
        },
      },
      account(tokens) {
        console.log(tokens);
      }

    })
  ],
  session: {
    strategy: "jwt"
  },
  callbacks: {
    signIn: async ({user, account, profile, email, credentials}) => {
      if (account?.provider === "google") {

        // TODO:
        //  After signing in with Google, check if the user is in the database.
        //  If the user is not in the database, reject the sign-in.
        //  If the user is in the database, return true to allow user to sign in.

        // TODO: Sample code below:

        const dbUser = await fetch(
          `${process.env.BACKEND_URL}/auth/google/callback/${account?.id_token}`,
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

}satisfies NextAuthConfig;

export const {auth, handlers, signIn, signOut} = NextAuth({
  ...authConfig
});
