import type { AssessmentSubmissionRuntimeViewV1 } from '@game-guild/grading';
import { describe, expect, it } from 'vitest';
import { readCodeRuntimeDefinition } from './runtime';
import type { CodingAssignmentContent } from './types';

function definition(): CodingAssignmentContent {
  return {
    Type: 'coding-assignment', Version: 1,
    Environment: { Language: 'cpp', Tools: 'clang', AllowStudentCreateFiles: false },
    Data: { Files: {} }, Tests: { Public: [], Private: [] },
    Grading: { MaxScore: 100 },
  };
}

function submission(adapterVersion: string) {
  const learner = definition();
  const instructor: CodingAssignmentContent = {
    ...definition(),
    Tests: { Public: [], Private: [{ kind: 'standard', Name: 'private', Stdout: '5' }] },
  };
  const value = {
    execution: {
      delivery: {
        itemOrder: ['code'],
        items: { code: {
          adapterKey: 'code-assessment-type', adapterVersion,
          learnerPayload: { definition: learner },
        } },
      },
      instructorVisibleContent: instructor,
    },
  };
  return { value, learner, instructor };
}

function read(value: unknown, instructor = false) {
  return readCodeRuntimeDefinition(value as AssessmentSubmissionRuntimeViewV1, instructor);
}

describe('readCodeRuntimeDefinition', () => {
  it.each(['1', '2'])('reads only the frozen definition for supported adapter %s', (version) => {
    const fixture = submission(version);
    expect(read(fixture.value)).toBe(fixture.learner);
    expect(read(fixture.value, true)).toBe(fixture.instructor);
    expect(read(fixture.value).Tests.Private).toEqual([]);
  });

  it.each(['', '0', '3', 'latest'])('rejects unsupported adapter %s', (version) => {
    const { value } = submission(version);
    expect(() => read(value)).toThrow('unavailable adapter version');
    expect(() => read(value, true)).toThrow('unavailable adapter version');
  });

  it('rejects a different adapter even when its version is supported', () => {
    const { value } = submission('2');
    value.execution.delivery.items.code.adapterKey = 'quiz-assessment-type';
    expect(() => read(value)).toThrow('unavailable adapter version');
  });

  it.each([{ order: [] }, { order: ['other'] }, { order: ['code', 'other'] }])(
    'rejects invalid delivery order $order', ({ order }) => {
      const { value } = submission('2');
      value.execution.delivery.itemOrder = order;
      expect(() => read(value)).toThrow('invalid delivery');
    });

  it('rejects a missing frozen Code item', () => {
    const { value } = submission('2');
    const missing = { execution: { ...value.execution,
      delivery: { itemOrder: ['code'], items: {} } } };
    expect(() => read(missing)).toThrow('unavailable adapter version');
  });

  it('requires the instructor definition without falling back to learner content', () => {
    const { value } = submission('2');
    const unavailable = { execution: { ...value.execution, instructorVisibleContent: null } };
    expect(() => read(unavailable, true)).toThrow('frozen Code definition is unavailable');
  });

  it('requires frozen learner content without falling back to instructor content', () => {
    const { value } = submission('2');
    const unavailable = { execution: { ...value.execution, delivery: {
      ...value.execution.delivery,
      items: { code: { ...value.execution.delivery.items.code, learnerPayload: {} } },
    } } };
    expect(() => read(unavailable)).toThrow('frozen Code definition is unavailable');
  });
});
