import { Configuration, CompetitionsApi, AuthApi } from '@/apinest';

const configuration = new Configuration({
  basePath: process.env.NEXT_PUBLIC_API_URL,
});

export const competitionsApi = new CompetitionsApi(configuration);

export const authApi = new AuthApi(configuration);
