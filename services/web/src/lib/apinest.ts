import { createClient } from '@hey-api/client-fetch';

const client = createClient({
  baseUrl: process.env.NEXT_PUBLIC_API_URL,
  throwOnError: false,
});
