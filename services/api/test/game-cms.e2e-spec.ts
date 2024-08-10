import {
  Configuration,
  ConfigurationParameters,
  GameApi,
  GameEntity,
  LocalSignInResponseDto,
} from '@game-guild/apiclient';
import { faker } from '@faker-js/faker';
import { CreateRandomUser } from './auth.e2e-spec';

describe('Game CMS (e2e)', () => {
  const basepath = 'http://localhost:8080';
  const apiConfig: ConfigurationParameters = {
    basePath: basepath,
    baseOptions: {
      validateStatus: () => true,
    },
  };
  jest.setTimeout(60000);

  // users
  let user1: LocalSignInResponseDto;
  let user2: LocalSignInResponseDto;

  let gameApi1: GameApi;
  let gameApi2: GameApi;

  // server have to be running
  beforeAll(async () => {
    // generate 2 random users
    user1 = await CreateRandomUser(apiConfig);
    user2 = await CreateRandomUser(apiConfig);

    // game api cms
    gameApi1 = new GameApi(
      new Configuration({ ...apiConfig, accessToken: user1.accessToken }),
    );
    gameApi2 = new GameApi(
      new Configuration({ ...apiConfig, accessToken: user2.accessToken }),
    );
  });

  // test game crud with permissions
  it.only('test ownership permission of a game', async () => {
    // create game
    const game1 = await gameApi1.createOneBaseGameControllerGameEntity({
      title: faker.lorem.words(2),
      summary: faker.lorem.words(10),
      body: faker.lorem.words(100),
      slug: faker.lorem.slug(2),
      visibility: 'DRAFT',
      thumbnail: faker.image.url(),
    } as GameEntity);
    expect(game1).toBeDefined();
    expect(game1.data).toBeDefined();
    expect(game1.data.id).toBeDefined();

    // update game
    const updatedGame = await gameApi1.updateOneBaseGameControllerGameEntity(
      game1.data.id,
      { title: faker.lorem.words(2) } as GameEntity,
    );
    expect(updatedGame).toBeDefined();
    expect(updatedGame.data).toBeDefined();
    expect(updatedGame.data.id).toBeDefined();

    // delete game
    const deletedGame = await gameApi1.deleteOneBaseGameControllerGameEntity(
      game1.data.id,
    );
    expect(deletedGame).toBeDefined();
  });
});
