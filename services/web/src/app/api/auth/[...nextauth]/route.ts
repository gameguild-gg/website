import NextAuth from 'next-auth';
import CredentialsProvider from 'next-auth/providers/credentials';
import GoogleProvider from "next-auth/providers/google";

const handler = NextAuth({
  providers: [
    GoogleProvider({
      clientId: process.env.GOOGLE_CLIENT_ID?process.env.GOOGLE_CLIENT_ID:"",
      clientSecret: process.env.GOOGLE_CLIENT_SECRET?process.env.GOOGLE_CLIENT_SECRET:"",
      authorization: {
        params: {
          prompt: "consent",
          access_type: "offline",
          response_type: "code"
        }
      },
    }, ),
    CredentialsProvider({
      credentials: {
        username: { label: 'Email', type: 'text' },
        password: { label: 'Password', type: 'password' },
      },
      async authorize(credentials, req) {
        if (!credentials?.username || !credentials?.password) return null;

        const { username, password } = credentials;

        const res = await fetch('', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            username,
            password,
          }),
        });

        if (res.status === 401) {
          return null;
        }

        const user = res.json();
        // const user = { id: "1", name: "Example", email: "example@example.com" }
        if (user) {
          return user;
        } else {
          return null;
        }
      },
    }),
  ],
});

export { handler as GET, handler as POST };
