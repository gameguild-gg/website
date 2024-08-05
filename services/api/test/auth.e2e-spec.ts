import {
  AuthApi,
  Configuration,
  ConfigurationParameters,
  LocalSignInResponseDto,
} from '@game-guild/apiclient';
import { faker } from '@faker-js/faker';
import { generatePassword } from './utils';

export async function CreateRandomUser(
  apiConfig: ConfigurationParameters,
): Promise<LocalSignInResponseDto> {
  // create user
  const authApi = new AuthApi(new Configuration(apiConfig));
  const username = faker.internet.userName().toLowerCase();
  const email = faker.internet.email().toLowerCase();
  const password = generatePassword(); // faker.internet.password();
  const createUserResponse =
    await authApi.authControllerSignUpWithEmailUsernamePassword({
      username,
      email,
      password,
    });

  // verify response
  expect(createUserResponse).toBeDefined();
  expect(createUserResponse.status).toBe(201);
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
  const basepath = 'http://localhost:8080';
  const apiConfig: ConfigurationParameters = {
    basePath: basepath,
    baseOptions: {
      validateStatus: () => true,
    },
  };
  jest.setTimeout(60000);

  // server have to be running
  beforeAll(async () => {
    jest.useFakeTimers();
  });

  // create user
  it('create user, get current user, delete user', async () => {
    const createUserData = await CreateRandomUser(apiConfig);
    const user = createUserData.user;

    // verify access and refresh tokens
    const accessToken = createUserData.accessToken;
    const refreshToken = createUserData.refreshToken;

    // tokens should be different
    expect(accessToken).not.toBe(refreshToken);

    // test current user from accesstoken
    const configClone = apiConfig;
    configClone.accessToken = accessToken;
    const authApiLogged = new AuthApi(new Configuration(configClone));
    const me = await authApiLogged.authControllerGetCurrentUser();
    expect(me).toBeDefined();
    expect(me.status).toBe(200);
    expect(me.data).toBeDefined();
    expect(me.data.username).toBe(user.username);
    expect(me.data.email).toBe(user.email);
    expect(me.data.passwordSalt).toBeUndefined();
    expect(me.data.passwordHash).toBeUndefined();
    expect(me.data.id).toBe(user.id);
  });

  it('test refresh token', async () => {
    const loginData = await CreateRandomUser(apiConfig);
    const configClone = apiConfig;
    configClone.accessToken = loginData.refreshToken;
    const authApiLogged = new AuthApi(new Configuration(configClone));

    const refreshTokenResponse =
      await authApiLogged.authControllerRefreshToken();
    expect(refreshTokenResponse).toBeDefined();
    expect(refreshTokenResponse.data).toBeDefined();

    const newLoginData = refreshTokenResponse.data;
    expect(newLoginData.user).toBeDefined();
    expect(newLoginData.accessToken).toBeDefined();
    expect(newLoginData.refreshToken).toBeDefined();
    // tokens should be different
    expect(newLoginData.accessToken).not.toBe(loginData.accessToken);
    expect(newLoginData.refreshToken).not.toBe(loginData.refreshToken);
    expect(newLoginData.user.id).toBe(loginData.user.id);
  });
});
