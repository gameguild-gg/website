// 'use server';
//
// import { HttpClient, httpClientFactory } from '@/lib/core/http';
// import { SignInFormState } from '@/lib/auth';
//
// export type SignInWithEmailAndPassword = {
//   signInWithEmailAndPassword: (
//     email: Readonly<string>,
//     password: Readonly<string>,
//   ) => Promise<any>;
// };
//
// export class SignInWithEmailAndPasswordGateway
//   implements SignInWithEmailAndPassword
// {
//   constructor(readonly httpClient: HttpClient) {}
//
//   async signInWithEmailAndPassword(
//     email: Readonly<string>,
//     password: Readonly<string>,
//   ): Promise<any> {
//     const response = await this.httpClient.request({
//       method: 'POST',
//       // TODO: Use the correct URL for the sign-in endpoint.
//       url: 'http://localhost:3000/auth/sign-in',
//       body: {
//         email: email,
//         password: password,
//       },
//     });
//
//     // TODO: Implement the response handling logic.
//     return {};
//   }
// }
//
// export async function signInWithEmailAndPassword(
//   previousState: SignInFormState,
//   formData: FormData,
// ): Promise<SignInFormState> {
//   // TODO: Implement sign-up with email and password.
//   const gateway = new SignInWithEmailAndPasswordGateway(httpClientFactory());
//
//   const email = formData.get('email') as string;
//   const password = formData.get('password') as string;
//
//   const response = await gateway.signInWithEmailAndPassword(email, password);
//
//   return Promise.resolve({});
// }
