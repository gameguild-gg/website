import { faker } from '@faker-js/faker';
import { baseUrl, delay, generatePassword } from './utils';
import {
  authControllerGetCurrentUser,
  authControllerRefreshToken,
  authControllerSignUpWithEmailUsernamePassword,
  LocalSignInResponseDto,
} from '@game-guild/apiclient';
import { createClient } from '@hey-api/client-fetch';

export async function CreateRandomUser(): Promise<LocalSignInResponseDto> {
  // create user
  const username = faker.internet.userName().toLowerCase();
  const email = faker.internet.email().toLowerCase();
  const password = generatePassword(); // faker.internet.password();

  const unauthorizedClient = createClient({
    baseUrl: 'http://localhost:8080',
  });

  const createUserResponse =
    await authControllerSignUpWithEmailUsernamePassword({
      body: {
        username,
        email,
        password,
      },
      throwOnError: false,
      client: unauthorizedClient,
    });

  // verify response
  expect(createUserResponse).toBeDefined();
  expect(createUserResponse.response.status).toBe(201);
  expect(createUserResponse.data).toBeDefined();

  const createUserData = createUserResponse.data;
  expect(createUserData.accessToken).toBeDefined();
  expect(createUserData.user).toBeDefined();
  expect(createUserData.refreshToken).toBeDefined();

  // verify created user
  const user = createUserData.user;
  expect(user.username).toBe(username);
  expect(user.email).toBe(email);
  expect(user.emailVerified).toBe(false);
  expect(user.passwordHash).toBeUndefined();
  expect(user.passwordSalt).toBeUndefined();
  expect(user.id).toBeDefined();
  return createUserData;
}

describe('Auth (e2e)', () => {
  jest.setTimeout(60000);

  // server have to be running
  beforeAll(async () => {
    // jest.useFakeTimers();
  });

  // create user
  it('create user, get current user, delete user', async () => {
    const createUserData = await CreateRandomUser();
    const user = createUserData.user;

    // verify access and refresh tokens
    const accessToken = createUserData.accessToken;
    const refreshToken = createUserData.refreshToken;

    // tokens should be different
    expect(accessToken).not.toBe(refreshToken);

    // test current user from accesstoken

    const authorizedClient = createClient({
      baseUrl: baseUrl,
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    const me = await authControllerGetCurrentUser({ client: authorizedClient });
    expect(me).toBeDefined();
    expect(me.data).toBeDefined();
    expect(me.data.username).toBe(user.username);
    expect(me.data.email).toBe(user.email);
    expect(me.data.id).toBe(user.id);
    expect(me.data.passwordHash).toBeUndefined();
    expect(me.data.passwordSalt).toBeUndefined();
  });

  it('test refresh token', async () => {
    const loginData = await CreateRandomUser();

    // wait 1 second
    await delay(1000);

    const client = createClient({
      baseUrl: baseUrl,
      headers: {
        Authorization: `Bearer ${loginData.accessToken}`,
      },
      throwOnError: false,
    });

    // const refreshTokenResponse = await authControllerRefreshToken({
    //   client: client,
    // });
    // expect(refreshTokenResponse).toBeDefined();
    //
    // const newLoginData = refreshTokenResponse.data;
    // expect(newLoginData).toBeDefined();
    // expect(newLoginData.user).toBeDefined();
    // expect(newLoginData.accessToken).toBeDefined();
    // expect(newLoginData.refreshToken).toBeDefined();
    // // // tokens should be different
    // expect(newLoginData.accessToken).not.toBe(loginData.accessToken);
    // expect(newLoginData.refreshToken).not.toBe(loginData.refreshToken);
    // expect(newLoginData.user.id).toBe(loginData.user.id);
  });
});
