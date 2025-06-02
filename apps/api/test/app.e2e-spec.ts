import { Test, TestingModule } from '@nestjs/testing';
import { HttpStatus, INestApplication } from '@nestjs/common';
import request from 'supertest';
import { AppModule } from '../src/app.module';

describe('AppController (e2e)', () => {
  let app: INestApplication;

  beforeEach(async () => {
    const moduleFixture: TestingModule = await Test.createTestingModule({
      imports: [AppModule],
    }).compile();

    app = moduleFixture.createNestApplication();
    await app.init();
  });

  it('should redirect to /documentation with 301 status', () => {
    return request(app.getHttpServer()).get('/').expect(HttpStatus.MOVED_PERMANENTLY).expect('Location', '/documentation');
  });

  afterEach(async () => {
    await app.close();
  });
});
