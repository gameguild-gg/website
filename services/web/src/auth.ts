import NextAuth, { type NextAuthConfig, User } from "next-auth";
import Credentials from "next-auth/providers/credentials";
import Google from "next-auth/providers/google";

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
      clientId: process.env.GOOGLE_CLIENT_ID,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET
    })
  ],
  session: {
    strategy: "jwt"
  }
} satisfies NextAuthConfig;

export const { auth, handlers, signIn, signOut } = NextAuth({
  ...authConfig
});
