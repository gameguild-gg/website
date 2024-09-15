import { AuthApi, CompetitionsApi, Configuration } from '@game-guild/apiclient';

const configuration = new Configuration({
  basePath: process.env.BACKEND_URL,
  // basePath: 'http://localhost:4000',
});

export const competitionsApi = new CompetitionsApi(configuration);

export const authApi = new AuthApi(configuration);
