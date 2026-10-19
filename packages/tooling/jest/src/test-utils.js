/**
 * Common test utilities for the monorepo
 * Import these utilities in your tests for common functionality
 */

import { afterAll, beforeAll, expect, jest } from '@jest/globals';

/**
 * Mock implementation of Next.js router
 */
export const mockRouter = {
  push: jest.fn(),
  replace: jest.fn(),
  prefetch: jest.fn(),
  back: jest.fn(),
  forward: jest.fn(),
  refresh: jest.fn(),
  pathname: '/',
  route: '/',
  query: {},
  asPath: '/',
  events: {
    on: jest.fn(),
    off: jest.fn(),
    emit: jest.fn(),
  },
};

/**
 * Mock fetch with configurable responses
 */
export const createMockFetch = (responses = {}) => {
  const defaultResponse = {
    ok: true,
    status: 200,
    json: () => Promise.resolve({}),
    text: () => Promise.resolve(''),
  };

  return jest.fn((url) => {
    const response = responses[url] || defaultResponse;
    return Promise.resolve(response);
  });
};

/**
 * Wait for async operations to complete
 */
export const waitFor = async (callback, { timeout = 5000, interval = 50 } = {}) => {
  const startTime = Date.now();

  while (Date.now() - startTime < timeout) {
    try {
      await callback();
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, interval));
    }
  }

  throw new Error(`waitFor timed out after ${timeout}ms`);
};

/**
 * Create a mock component for testing
 */
export const createMockComponent = (name, props = {}) => {
  const MockComponent = jest.fn(() => {
    // Create a simple mock DOM element
    return {
      type: 'div',
      props: { 'data-testid': name, ...props },
      children: [],
    };
  });
  MockComponent.displayName = name;
  return MockComponent;
};

/**
 * Mock local storage
 */
export const mockLocalStorage = (() => {
  let store = {};

  return {
    getItem: jest.fn((key) => store[key] || null),
    setItem: jest.fn((key, value) => {
      store[key] = value.toString();
    }),
    removeItem: jest.fn((key) => {
      delete store[key];
    }),
    clear: jest.fn(() => {
      store = {};
    }),
    length: 0,
    key: jest.fn((index) => Object.keys(store)[index] || null),
  };
})();

/**
 * Mock session storage
 */
export const mockSessionStorage = (() => {
  let store = {};

  return {
    getItem: jest.fn((key) => store[key] || null),
    setItem: jest.fn((key, value) => {
      store[key] = value.toString();
    }),
    removeItem: jest.fn((key) => {
      delete store[key];
    }),
    clear: jest.fn(() => {
      store = {};
    }),
    length: 0,
    key: jest.fn((index) => Object.keys(store)[index] || null),
  };
})();

/**
 * Mock environment variables
 */
export const mockEnv = (variables) => {
  const originalEnv = { ...process.env };

  beforeAll(() => {
    Object.assign(process.env, variables);
  });

  afterAll(() => {
    process.env = originalEnv;
  });
};

/**
 * Create a mock implementation of a service
 */
export const createMockService = (methods) => {
  const mock = {};

  Object.keys(methods).forEach((method) => {
    mock[method] = jest.fn(methods[method]);
  });

  return mock;
};

/**
 * Performance test helpers
 */
export const performanceHelpers = {
  /**
   * Measure execution time of a function
   */
  measureTime: async (fn) => {
    const start = performance.now();
    const result = await fn();
    const end = performance.now();

    return {
      result,
      time: end - start,
    };
  },

  /**
   * Assert that a function completes within a time limit
   */
  expectTimeLessThan: async (fn, maxTime) => {
    const { time } = await performanceHelpers.measureTime(fn);
    expect(time).toBeLessThan(maxTime);
  },

  /**
   * Run a function multiple times and get average execution time
   */
  benchmark: async (fn, iterations = 10) => {
    const times = [];

    for (let i = 0; i < iterations; i++) {
      const { time } = await performanceHelpers.measureTime(fn);
      times.push(time);
    }

    return {
      average: times.reduce((sum, time) => sum + time, 0) / times.length,
      min: Math.min(...times),
      max: Math.max(...times),
      times,
    };
  },
};
