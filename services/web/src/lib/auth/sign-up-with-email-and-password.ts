'use server';

import { HttpClient, httpClientFactory } from '@/lib/core/http';
import { SignUpFormState } from '@/lib/auth';

export type SignUpWithEmailAndPassword = {
  signUpWithEmailAndPassword: (
    email: Readonly<string>,
    password: Readonly<string>,
  ) => Promise<any>;
};

export class SignUpWithEmailAndPasswordGateway
  implements SignUpWithEmailAndPassword
{
  constructor(readonly httpClient: HttpClient) {}

  async signUpWithEmailAndPassword(
    email: Readonly<string>,
    password: Readonly<string>,
  ): Promise<any> {
    const response = await this.httpClient.request({
      method: 'POST',
      // TODO: Use the correct URL for the sign-up endpoint.
      url: 'http://localhost:3000/auth/sign-up',
      body: {
        email: email,
        password: password,
      },
    });

    // TODO: Implement the response handling logic.
    return {};
  }
}

export async function signUpWithEmailAndPassword(
  previousState: SignUpFormState,
  formData: FormData,
): Promise<SignUpFormState> {
  // TODO: Implement sign-up with email and password.
  const gateway = new SignUpWithEmailAndPasswordGateway(httpClientFactory());

  const email = formData.get('email') as string;
  const password = formData.get('password') as string;

  const response = await gateway.signUpWithEmailAndPassword(email, password);

  return Promise.resolve({});
}
