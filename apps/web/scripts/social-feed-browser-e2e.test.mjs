import assert from 'node:assert/strict';
import test from 'node:test';

import { assertSocialEvidence, REQUIRED_SOCIAL_EVIDENCE } from './social-feed-browser-e2e.mjs';

test('social feed browser gate requires every production interaction', () => {
  const evidence = Object.fromEntries(REQUIRED_SOCIAL_EVIDENCE.map((key) => [key, true]));
  assert.doesNotThrow(() => assertSocialEvidence(evidence));
});

test('social feed browser gate rejects incomplete evidence', () => {
  const evidence = Object.fromEntries(REQUIRED_SOCIAL_EVIDENCE.map((key) => [key, true]));
  evidence.persistedAfterReload = false;
  assert.throws(() => assertSocialEvidence(evidence), /persistedAfterReload/);
});
