'use server';

import {httpClientFactory} from "@/lib/core/http";


export async function GetWeb3SignInChallenge() {
  const httpClient = httpClientFactory();

  const response = await httpClient.request<any>({
    url: '/api/auth/web3/sign-in',
    method: 'GET',
  });


}
