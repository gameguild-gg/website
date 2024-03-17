import NextAuth, { type NextAuthConfig, User } from "next-auth";
import Credentials from "next-auth/providers/credentials";

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
    })
  ],
  session: {
    strategy: "jwt"
  }
} satisfies NextAuthConfig;

export const { auth, handlers, signIn, signOut } = NextAuth({
  ...authConfig
});

