declare module 'next-auth' {
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
