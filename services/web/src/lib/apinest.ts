import { AuthApi, CompetitionsApi, Configuration } from '@game-guild/apiclient';

const configuration = new Configuration({
  // basePath: process.env.NEST_JS_BACKEND_URL,
  basePath: 'http://localhost:8080',
});

export const competitionsApi = new CompetitionsApi(configuration);

export const authApi = new AuthApi(configuration);
