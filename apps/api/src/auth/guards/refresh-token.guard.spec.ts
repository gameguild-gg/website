import { RefreshTokenGuard } from './refresh-token.guard';

describe('RefreshTokenGuard', () => {
  it('should be defined', () => {
    expect(new RefreshTokenGuard()).toBeDefined();
  });
});
