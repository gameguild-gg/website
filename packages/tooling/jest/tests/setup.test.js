import { afterEach, expect, jest, test } from '@jest/globals';

const cleanupProbe = jest.fn();

afterEach(() => {
  expect(cleanupProbe).not.toHaveBeenCalled();
  expect(document.body.innerHTML).toBe('');
});

test('browser setup works with automatic Jest globals disabled', () => {
  expect(globalThis.jest).toBeUndefined();
  expect(globalThis.afterEach).toBeUndefined();
  expect(globalThis.expect).toBeUndefined();
  expect(process.env.NODE_ENV).toBe('test');
  expect(globalThis.testUtils.wait).toBeInstanceOf(Function);
});

test('media and observer helpers retain their browser contracts', () => {
  const query = window.matchMedia('(min-width: 1px)');
  expect(query.media).toBe('(min-width: 1px)');
  expect(query.matches).toBe(false);
  for (const name of ['addListener', 'removeListener', 'addEventListener', 'removeEventListener', 'dispatchEvent']) {
    expect(jest.isMockFunction(query[name])).toBe(true);
  }
  for (const Observer of [IntersectionObserver, ResizeObserver]) {
    const observer = new Observer();
    expect(observer.observe()).toBeNull();
    expect(observer.unobserve()).toBeNull();
    expect(observer.disconnect()).toBeNull();
  }
});

test('timer helpers use the running Jest timer implementation', () => {
  globalThis.testUtils.mockTimers();
  try {
    const completed = jest.fn();
    setTimeout(completed, 10);
    jest.advanceTimersByTime(10);
    expect(completed).toHaveBeenCalledTimes(1);
  } finally {
    globalThis.testUtils.restoreTimers();
  }
});

test('console helpers suppress and restore the original methods', () => {
  const originalLog = console.log;
  globalThis.testUtils.suppressConsole();
  try {
    console.log('synthetic log');
    expect(console.log).toHaveBeenCalledWith('synthetic log');
  } finally {
    globalThis.testUtils.restoreConsole();
  }
  expect(console.log).toBe(originalLog);
});

test('registered cleanup clears mocks and DOM after the test', () => {
  cleanupProbe('synthetic');
  document.body.innerHTML = '<p>synthetic</p>';
  expect(cleanupProbe).toHaveBeenCalledTimes(1);
  expect(document.body.textContent).toBe('synthetic');
});
