import NextAuth, { type NextAuthConfig, User } from "next-auth";
import Credentials from "next-auth/providers/credentials";
import Google from "next-auth/providers/google";
import { environment } from "@/lib/environment";

export const authConfig = {
  pages: {
    signIn: "/sign-in"
  },
  providers: [
    Credentials({
      id: "local",
      name: "Local",
      credentials: {
        email: { label: "Email", type: "text" },
        password: { label: "Password", type: "password" }
      },
      authorize: async (credentials) => {
        // TODO Implement the authorize function here.
        const user: User = {
          id: "1",
          name: "Test User",
          email: "credentials.email"
        };
        return user;
      }
    }),
    Google({
      clientId: environment.GoogleClientId,
      clientSecret: environment.GoogleClientSecret,
      authorization: {
        params: {
          request_uri: `${process.env.BACKEND_URL}/auth/google/callback`
        }
      }
    })
  ],
  session: {
    strategy: "jwt"
  },
  callbacks: {
    signIn({ user, account, profile, email, credentials }) {
      if (account?.provider === "google") {

        // TODO:
        // After signing in with Google, check if the user is in the database.
        // If the user is not in the database, reject the sign-in.
        // If the user is in the database, return true to allow sign in.

        /* const dbUser = await fetch(
          `${process.env.BACKEND_URL}/auth/google/token?token=${account?.id_token}`,
          {
            method: "GET",
            headers: {
              "Content-Type": "application/json",
            },
          },
        ).then((r) => r.json());

        if (!dbUser?.data?.user) return false;

        user.id = dbUser?.data?.user?.id;
        user.email = dbUser?.data?.user?.email;
        user.firstName = dbUser?.data?.user?.firstName;
        user.lastName = dbUser?.data?.user?.lastName;
        user.avatar = dbUser?.data?.user?.avatar;
        user.isEmailVerified = dbUser?.data?.user?.isEmailVerified;
        user.isPhoneVerified = dbUser?.data?.user?.isPhoneVerified;
        user.token = dbUser?.data?.accessToken;
        return true; */
      }

      // Return true to allow sign in.
      return false;
    }
  }

} satisfies NextAuthConfig;

export const { auth, handlers, signIn, signOut } = NextAuth({
  ...authConfig
});
