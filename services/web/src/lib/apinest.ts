import { createClient } from '@hey-api/client-axios';

const client = createClient({
  baseURL: process.env.NEXT_PUBLIC_API_URL,
  throwOnError: false,
});
