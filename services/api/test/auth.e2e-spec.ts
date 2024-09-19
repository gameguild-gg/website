import { faker } from '@faker-js/faker';
import { delay, generatePassword } from './utils';
import { Api, AuthApi } from '@game-guild/apiclient';
import LocalSignInResponseDto = Api.LocalSignInResponseDto;
import * as process from 'node:process';

const api = new AuthApi({ basePath: process.env.HOST_BACK_URL });

export async function CreateRandomUser(): Promise<LocalSignInResponseDto> {
  // create user
  const username = faker.internet.userName().toLowerCase();
  const email = faker.internet.email().toLowerCase();
  const password = generatePassword(); // faker.internet.password();

  const createUserResponse =
    await api.authControllerSignUpWithEmailUsernamePassword({
      username,
      email,
      password,
    });

  // verify response
  expect(createUserResponse).toBeDefined();

  const createUserData = createUserResponse;
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

    const headers = {
      Authorization: `Bearer ${accessToken}`,
    };

    const me = await api.authControllerGetCurrentUser({ headers });
    expect(me).toBeDefined();
    expect(me.username).toBe(user.username);
    expect(me.email).toBe(user.email);
    expect(me.id).toBe(user.id);
    expect(me.passwordHash).toBeUndefined();
  });

  it.only('test refresh token', async () => {
    const loginData = await CreateRandomUser();

    // wait 1 second
    await delay(1000);

    const headers = {
      Authorization: `Bearer ${loginData.refreshToken}`,
    };

    try {
      const refreshTokenResponse = await api.authControllerRefreshToken({
        headers,
      });

      expect(refreshTokenResponse).toBeDefined();

      const newLoginData = refreshTokenResponse;
      expect(newLoginData).toBeDefined();
      expect(newLoginData.user).toBeDefined();
      expect(newLoginData.accessToken).toBeDefined();
      expect(newLoginData.refreshToken).toBeDefined();
      // // tokens should be different
      expect(newLoginData.accessToken).not.toBe(loginData.accessToken);
      expect(newLoginData.refreshToken).not.toBe(loginData.refreshToken);
      expect(newLoginData.user.id).toBe(loginData.user.id);
    } catch (e) {
      console.log(JSON.stringify(e));
    }
  });
});
