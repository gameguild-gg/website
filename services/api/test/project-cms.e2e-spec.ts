import { faker } from '@faker-js/faker';
import { CreateRandomUser } from './auth.e2e-spec';
import {
  createOneBaseProjectControllerProjectEntity,
  deleteOneBaseProjectControllerProjectEntity,
  LocalSignInResponseDto,
  ProjectEntity,
  updateOneBaseProjectControllerProjectEntity,
} from '@game-guild/apiclient';
import { Client, createClient } from '@hey-api/client-fetch';
import { baseUrl } from './utils';

describe('Project CMS (e2e)', () => {
  jest.setTimeout(60000);

  // users
  let user1: LocalSignInResponseDto;
  let user2: LocalSignInResponseDto;

  let gameApi1: Client;
  let gameApi2: Client;

  // server have to be running
  beforeAll(async () => {
    // generate 2 random users
    user1 = await CreateRandomUser();
    user2 = await CreateRandomUser();

    gameApi1 = createClient({
      baseUrl: baseUrl,
      headers: {
        Authorization: `Bearer ${user1.accessToken}`,
      },
    });

    gameApi2 = createClient({
      baseUrl: baseUrl,
      headers: {
        Authorization: `Bearer ${user2.accessToken}`,
      },
    });
  });

  it('test create game', async () => {
    const gameData = {
      title: faker.lorem.words(2),
      summary: faker.lorem.words(10),
      body: faker.lorem.words(100),
      slug: faker.lorem.slug(2),
      visibility: 'DRAFT',
      thumbnail: faker.image.url(),
    } as ProjectEntity;
    const game = await createOneBaseProjectControllerProjectEntity({
      body: gameData,
      client: gameApi1,
    });
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
    const game1 = await createOneBaseProjectControllerProjectEntity({
      body: {
        title: faker.lorem.words(2),
        summary: faker.lorem.words(10),
        body: faker.lorem.words(100),
        slug: faker.lorem.slug(2),
        visibility: 'DRAFT',
        thumbnail: faker.image.url(),
      } as ProjectEntity,
      client: gameApi1,
    });
    expect(game1).toBeDefined();
    expect(game1.data).toBeDefined();
    expect(game1.data.id).toBeDefined();

    // update game
    const updatedGame = await updateOneBaseProjectControllerProjectEntity({
      body: {
        title: faker.lorem.words(2),
      } as ProjectEntity,
      path: {
        id: game1.data.id,
      },
      client: gameApi1,
    });
    expect(updatedGame).toBeDefined();
    expect(updatedGame.data).toBeDefined();
    expect(updatedGame.data.id).toBeDefined();

    // delete game
    const deletedGame = await deleteOneBaseProjectControllerProjectEntity({
      path: {
        id: game1.data.id,
      },
      client: gameApi1,
    });
    expect(deletedGame).toBeDefined();
  });

  // todo: test transfer ownership
});
