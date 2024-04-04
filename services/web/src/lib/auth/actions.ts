"use server";

export type SignInFormState = {};

export type SignUpFormState = {};

export type ForgotPasswordFormState = {};

export async function signInWithEmailAndPassword(previousState: SignInFormState, formData: FormData): Promise<SignInFormState> {
  // TODO: Implement sign-in with email and password.
  // try {
  //   await signIn(
  //     "local",
  //     {
  //       email: formData.get("email"),
  //       password: formData.get("password"),
  //       redirectTo: "/"
  //     }
  //   );
  // } catch (notFound) {
  //   if (notFound instanceof AuthError) {
  //     switch (notFound.type) {
  //       default: {
  //
  //       }
  //     }
  //   }
  // }
  return Promise.resolve({});
}

export async function signInWithGoogle() {
  await signIn('google');
}


export async function signUpWithEmailAndPassword(previousState: SignUpFormState, formData: FormData): Promise<SignUpFormState> {
  // TODO: Implement sign-up with email and password.
  return Promise.resolve({});
}

export async function forgotPassword(previousState: SignInFormState, formData: FormData): Promise<ForgotPasswordFormState> {
  // TODO: Implement sign-up with email and password.
  return Promise.resolve({});
}

// export const fetchWeb3SignInChallenge = async () => {
//   // TODO: should be an const with the API_HOST environment variable.
//   const response = await fetch(
//     `http://${process.env.API_HOST}/api/auth/web3/challenge`,
//     {}
//   );
//
//   if (!response.ok) {
//     throw new Error("Failed to fetch Web3 sign-in challenge.");
//   }
//
//   const { message } = await response.json();
//
//   return message;
// };
//
// export type Web3SignInChallengeValidationPayload = {
//   accountAddress: string;
//   message: string;
//   signature: string;
// }
//
// export const validateWeb3SignInChallenge = async (data: Web3SignInChallengeValidationPayload) => {
//   const response = await fetch(
//     `http://${process.env.API_HOST}/api/auth/web3/validate`,
//     {
//       method: "POST",
//       headers: {
//         "Content-Type": "application/json"
//       },
//       body: JSON.stringify({
//         accountAddress: data.accountAddress,
//         signature: data.signature,
//         message: data.message
//       })
//     }
//   );
//
//   if (!response.ok) {
//     throw new Error("Failed to validate Web3 sign-in challenge.");
//   }
//
//   const { user, accessToken, refreshToken } = await response.json();
//
//   return { user, accessToken, refreshToken };
// };
