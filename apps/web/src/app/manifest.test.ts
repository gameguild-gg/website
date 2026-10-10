import { describe, expect, it } from 'vitest';
import manifest from './manifest';

describe('Web app manifest', () => {
  it('uses the approved GameGuild name for full and compact displays', async () => {
    const result = await manifest();

    expect(result.name).toBe('GameGuild');
    expect(result.short_name).toBe('GameGuild');
  });

  it('keeps the existing launch route and vector brand icon', async () => {
    const result = await manifest();

    expect(result.start_url).toBe('/');
    expect(result.icons).toEqual([{ src: '/favicon.svg', sizes: 'any', type: 'image/svg+xml' }]);
  });
});
