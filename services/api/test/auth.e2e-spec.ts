import { AuthApi, Configuration } from '@game-guild/apiclient';
import { faker } from '@faker-js/faker';
import { generatePassword } from './utils';

// todo: the API

describe('Auth (e2e)', () => {
  let apiConfig: Configuration;
  const basepath = 'http://localhost:8080';
  jest.setTimeout(60000);

  // server have to be running
  beforeAll(async () => {
    jest.useFakeTimers();
    apiConfig = new Configuration({
      basePath: basepath,
      baseOptions: {
        validateStatus: () => true,
      },
    });
  });

  // create user
  it('create user, get current user, delete user', async () => {
    // create user
    const authApi = new AuthApi(apiConfig);
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
    expect(createUserResponse.data.accessToken).toBeDefined();
    expect(createUserResponse.data.user).toBeDefined();
    expect(createUserResponse.data.refreshToken).toBeDefined();

    // verify created user
    const user = createUserResponse.data.user;
    expect(user.username).toBe(username);
    expect(user.email).toBe(email);
    expect(user.emailVerified).toBe(false);
    expect(user.passwordHash).toBeUndefined();
    expect(user.passwordSalt).toBeUndefined();
    expect(user.id).toBeDefined();

    // verify access and refresh tokens
    const accessToken = createUserResponse.data.accessToken;
    const refreshToken = createUserResponse.data.refreshToken;
  });
});
