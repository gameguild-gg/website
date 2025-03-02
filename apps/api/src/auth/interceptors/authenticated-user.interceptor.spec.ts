import { AuthenticatedUserInterceptor } from './authenticated-user.interceptor';

describe('AuthenticatedUserInterceptor', () => {
  it('should be defined', () => {
    expect(new AuthenticatedUserInterceptor()).toBeDefined();
  });
});
