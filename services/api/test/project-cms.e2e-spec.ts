import {
  Configuration,
  ConfigurationParameters,
  LocalSignInResponseDto,
  ProjectApi,
  ProjectEntity,
} from '@game-guild/apiclient';
import { faker } from '@faker-js/faker';
import { CreateRandomUser } from './auth.e2e-spec';

describe('Project CMS (e2e)', () => {
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

  let gameApi1: ProjectApi;
  let gameApi2: ProjectApi;

  // server have to be running
  beforeAll(async () => {
    // generate 2 random users
    user1 = await CreateRandomUser(apiConfig);
    user2 = await CreateRandomUser(apiConfig);

    // game api cms
    gameApi1 = new ProjectApi(
      new Configuration({ ...apiConfig, accessToken: user1.accessToken }),
    );
    gameApi2 = new ProjectApi(
      new Configuration({ ...apiConfig, accessToken: user2.accessToken }),
    );
  });

  it.only('test create game', async () => {
    const gameData = {
      title: faker.lorem.words(2),
      summary: faker.lorem.words(10),
      body: faker.lorem.words(100),
      slug: faker.lorem.slug(2),
      visibility: 'DRAFT',
      thumbnail: faker.image.url(),
    } as ProjectEntity;
    const game =
      await gameApi1.createOneBaseProjectControllerProjectEntity(gameData);
    expect(game).toBeDefined();
    expect(game.data).toBeDefined();
    expect(game.data.id).toBeDefined();
    expect(game.data.createdAt).toBeDefined();
    expect(game.data.updatedAt).toBeDefined();
    expect(game.data.title).toBe(gameData.title);
    expect(game.data.summary).toBe(gameData.summary);
    expect(game.data.body).toBe(gameData.body);
    expect(game.data.slug).toBe(gameData.slug);
    expect(game.data.visibility).toBe(gameData.visibility);
    expect(game.data.thumbnail).toBe(gameData.thumbnail);
    expect(game.data.owner.id).toBe(user1.user.id);
    expect(game.data.editors[0].id).toBe(user1.user.id);
  });

  // test game crud with permissions
  it('test ownership permission of a game', async () => {
    // create game
    const game1 = await gameApi1.createOneBaseProjectControllerProjectEntity({
      title: faker.lorem.words(2),
      summary: faker.lorem.words(10),
      body: faker.lorem.words(100),
      slug: faker.lorem.slug(2),
      visibility: 'DRAFT',
      thumbnail: faker.image.url(),
    } as ProjectEntity);
    expect(game1).toBeDefined();
    expect(game1.data).toBeDefined();
    expect(game1.data.id).toBeDefined();

    // update game
    const updatedGame =
      await gameApi1.updateOneBaseProjectControllerProjectEntity(
        game1.data.id,
        { title: faker.lorem.words(2) } as ProjectEntity,
      );
    expect(updatedGame).toBeDefined();
    expect(updatedGame.data).toBeDefined();
    expect(updatedGame.data.id).toBeDefined();

    // delete game
    const deletedGame =
      await gameApi1.deleteOneBaseProjectControllerProjectEntity(game1.data.id);
    expect(deletedGame).toBeDefined();
  });
});
