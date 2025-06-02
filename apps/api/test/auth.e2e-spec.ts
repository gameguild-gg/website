// import { faker } from '@faker-js/faker';
// import { delay, generatePassword } from './utils';
// import { Api, AuthApi } from '@game-guild/apiclient';
// import * as process from 'node:process';
// import LocalSignInResponseDto = Api.LocalSignInResponseDto;
//
// const api = new AuthApi({ basePath: process.env.HOST_BACK_URL });
//
// export async function CreateRandomUser(): Promise<LocalSignInResponseDto> {
//   // create user
//   const username = faker.internet.userName().toLowerCase();
//   const email = faker.internet.email().toLowerCase();
//   const password = generatePassword(); // faker.internet.password();
//
//   const createUserResponse =
//     await api.authControllerSignUpWithEmailUsernamePassword({
//       username,
//       email,
//       password,
//     });
//
//   // verify response
//   expect(createUserResponse).toBeDefined();
//   expect(createUserResponse.status).toBe(201);
//   expect(createUserResponse.body).toBeDefined();
//
//   const body = createUserResponse.body as LocalSignInResponseDto;
//   expect(body.accessToken).toBeDefined();
//   expect(body.user).toBeDefined();
//   expect(body.refreshToken).toBeDefined();
//
//   // verify created user
//   const user = body.user;
//   expect(user.username).toBe(username);
//   expect(user.email).toBe(email);
//   expect(user.emailVerified).toBe(false);
//   expect(user.passwordHash).toBeUndefined();
//   expect(user.passwordSalt).toBeUndefined();
//   expect(user.id).toBeDefined();
//   return body;
// }
//
// describe('Auth (e2e)', () => {
//   jest.setTimeout(60000);
//
//   // server have to be running
//   beforeAll(async () => {
//     // jest.useFakeTimers();
//   });
//
//   // create user
//   it('create user, get current user, delete user', async () => {
//     const createUserData = await CreateRandomUser();
//     const user = createUserData.user;
//
//     // verify access and refresh tokens
//     const accessToken = createUserData.accessToken;
//     const refreshToken = createUserData.refreshToken;
//
//     // tokens should be different
//     expect(accessToken).not.toBe(refreshToken);
//
//     // test current user from accesstoken
//
//     const headers = {
//       Authorization: `Bearer ${accessToken}`,
//     };
//
//     const response = await api.authControllerGetCurrentUser({ headers });
//
//     expect(response).toBeDefined();
//     expect(response.status).toBe(200);
//
//     const me = response.body as Api.UserEntity;
//     expect(me).toBeDefined();
//
//     expect(me.username).toBe(user.username);
//     expect(me.email).toBe(user.email);
//     expect(me.id).toBe(user.id);
//     expect(me.passwordHash).toBeUndefined();
//     expect(me.passwordSalt).toBeUndefined();
//   });
//
//   it.only('test refresh token', async () => {
//     const loginData = await CreateRandomUser();
//
//     // wait 1 second
//     await delay(1000);
//
//     const headers = {
//       Authorization: `Bearer ${loginData.refreshToken}`,
//     };
//
//     try {
//       const refreshTokenResponse = await api.authControllerRefreshToken({
//         headers,
//       }); // it is breaking here
//
//       expect(refreshTokenResponse).toBeDefined();
//
//       const newLoginData =
//         refreshTokenResponse.body as Api.LocalSignInResponseDto;
//       expect(newLoginData).toBeDefined();
//
//       expect(newLoginData.user).toBeDefined();
//       expect(newLoginData.accessToken).toBeDefined();
//       expect(newLoginData.refreshToken).toBeDefined();
//       // // tokens should be different
//       expect(newLoginData.accessToken).not.toBe(loginData.accessToken);
//       expect(newLoginData.refreshToken).not.toBe(loginData.refreshToken);
//       expect(newLoginData.user.id).toBe(loginData.user.id);
//     } catch (e) {
//       console.log(JSON.stringify(e));
//       expect(e).toBeUndefined();
//     }
//   });
// });
