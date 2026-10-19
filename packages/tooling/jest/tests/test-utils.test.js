import { afterAll, describe, expect, jest, test } from '@jest/globals';
import {
  createMockComponent,
  createMockFetch,
  createMockService,
  mockEnv,
  mockLocalStorage,
  mockRouter,
  mockSessionStorage,
  performanceHelpers,
  waitFor,
} from '../src/test-utils.js';

test('uses imported Jest APIs with automatic globals disabled', () => {
  for (const name of ['jest', 'expect', 'beforeAll', 'afterAll', 'afterEach']) {
    expect(globalThis[name]).toBeUndefined();
  }
});

test('router methods are real Jest mocks', () => {
  const methods = [mockRouter.push, mockRouter.replace, mockRouter.prefetch, mockRouter.back,
    mockRouter.forward, mockRouter.refresh, ...Object.values(mockRouter.events)];
  for (const method of methods) {
    expect(jest.isMockFunction(method)).toBe(true);
  }
  mockRouter.push('/synthetic');
  expect(mockRouter.push).toHaveBeenCalledWith('/synthetic');
});

test('fetch helpers return configured and default responses', async () => {
  const response = { ok: false, status: 418 };
  const fetch = createMockFetch({ '/configured': response });
  await expect(fetch('/configured')).resolves.toBe(response);
  const fallback = await fetch('/missing');
  expect(fallback).toMatchObject({ ok: true, status: 200 });
  await expect(fallback.json()).resolves.toEqual({});
  await expect(fallback.text()).resolves.toBe('');
});

test('component helpers retain props and display name', () => {
  const component = createMockComponent('synthetic', { title: 'example' });
  expect(component.displayName).toBe('synthetic');
  expect(component()).toEqual({
    type: 'div', props: { 'data-testid': 'synthetic', title: 'example' }, children: [],
  });
  expect(component).toHaveBeenCalledTimes(1);
});

test('storage helpers retain independent values and clear their stores', () => {
  for (const storage of [mockLocalStorage, mockSessionStorage]) storage.clear();
  mockLocalStorage.setItem('key', 7);
  mockSessionStorage.setItem('key', 'session');
  expect(mockLocalStorage.getItem('key')).toBe('7');
  expect(mockSessionStorage.getItem('key')).toBe('session');
  expect(mockLocalStorage.key(0)).toBe('key');
  mockLocalStorage.removeItem('key');
  expect(mockLocalStorage.getItem('key')).toBeNull();
  expect(mockSessionStorage.getItem('key')).toBe('session');
  mockSessionStorage.clear();
  expect(mockSessionStorage.getItem('key')).toBeNull();
});

test('service helpers preserve method arguments and return values', () => {
  const service = createMockService({ twice: (value) => value * 2 });
  expect(service.twice(3)).toBe(6);
  expect(service.twice).toHaveBeenCalledWith(3);
});

test('wait helper accepts a successful async callback', async () => {
  const callback = jest.fn().mockResolvedValue('done');
  await expect(waitFor(callback)).resolves.toBeUndefined();
  expect(callback).toHaveBeenCalledTimes(1);
});

test('wait helper retries a rejected callback', async () => {
  const callback = jest.fn().mockRejectedValueOnce(new Error('retry')).mockResolvedValue('done');
  await expect(waitFor(callback, { timeout: 5000, interval: 0 })).resolves.toBeUndefined();
  expect(callback).toHaveBeenCalledTimes(2);
});

test('wait helper reports an exhausted deadline', async () => {
  const callback = jest.fn();
  await expect(waitFor(callback, { timeout: 0 })).rejects.toThrow('waitFor timed out after 0ms');
  expect(callback).not.toHaveBeenCalled();
});

test('measurement helper returns the real async operation result', async () => {
  const callback = jest.fn().mockResolvedValue('result');
  const measured = await performanceHelpers.measureTime(callback);
  expect(measured.result).toBe('result');
  expect(Number.isFinite(measured.time)).toBe(true);
  expect(measured.time).toBeGreaterThanOrEqual(0);
  expect(callback).toHaveBeenCalledTimes(1);
});

test('time assertion uses imported expect for an accepted measurement', async () => {
  jest.spyOn(performanceHelpers, 'measureTime').mockResolvedValue({ result: null, time: 7 });
  await expect(performanceHelpers.expectTimeLessThan(jest.fn(), 8)).resolves.toBeUndefined();
});

test('time assertion rejects a measurement above the limit', async () => {
  jest.spyOn(performanceHelpers, 'measureTime').mockResolvedValue({ result: null, time: 7 });
  await expect(performanceHelpers.expectTimeLessThan(jest.fn(), 6)).rejects.toThrow();
});

test('benchmark aggregates measurements and preserves callback forwarding', async () => {
  const callback = jest.fn();
  const measure = jest.spyOn(performanceHelpers, 'measureTime')
    .mockResolvedValueOnce({ result: null, time: 1 })
    .mockResolvedValueOnce({ result: null, time: 3 })
    .mockResolvedValueOnce({ result: null, time: 5 });
  await expect(performanceHelpers.benchmark(callback, 3)).resolves.toEqual({
    average: 3, min: 1, max: 5, times: [1, 3, 5],
  });
  expect(measure).toHaveBeenCalledTimes(3);
  expect(measure).toHaveBeenCalledWith(callback);
});

describe('environment lifecycle', () => {
  const key = 'GAMEGUILD_JEST_HELPER_SYNTHETIC';
  const original = process.env[key];
  afterAll(() => expect(process.env[key]).toBe(original));

  describe('with an override', () => {
    mockEnv({ [key]: 'temporary' });

    test('environment helper installs and restores values through real hooks', () => {
      expect(process.env[key]).toBe('temporary');
    });
  });
});
