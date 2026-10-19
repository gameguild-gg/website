/**
 * Global Jest setup for all test environments
 * This file configures common test environment settings across the monorepo
 */

import { afterEach, jest } from '@jest/globals';

// Global test environment configuration
global.process.env.NODE_ENV = 'test';

// Common test utilities available globally
global.testUtils = {
  // Mock timers utilities
  mockTimers: () => {
    jest.useFakeTimers();
  },
  restoreTimers: () => {
    jest.useRealTimers();
  },

  // Common test helpers
  wait: (ms = 0) => new Promise((resolve) => setTimeout(resolve, ms)),

  // Mock console methods for cleaner test output
  suppressConsole: () => {
    global.originalConsole = { ...console };
    global.console = {
      ...console,
      log: jest.fn(),
      warn: jest.fn(),
      error: jest.fn(),
      debug: jest.fn(),
    };
  },

  restoreConsole: () => {
    if (global.originalConsole) {
      global.console = global.originalConsole;
    }
  },
};

// Global mocks for common APIs
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: jest.fn().mockImplementation((query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: jest.fn(), // deprecated
    removeListener: jest.fn(), // deprecated
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    dispatchEvent: jest.fn(),
  })),
});

// Mock IntersectionObserver
global.IntersectionObserver = class IntersectionObserver {
  constructor() {}

  observe() {
    return null;
  }

  disconnect() {
    return null;
  }

  unobserve() {
    return null;
  }
};

// Mock ResizeObserver
global.ResizeObserver = class ResizeObserver {
  constructor() {}

  observe() {
    return null;
  }

  disconnect() {
    return null;
  }

  unobserve() {
    return null;
  }
};

// Setup for performance testing
if (typeof performance === 'undefined') {
  global.performance = {
    now: jest.fn(() => Date.now()),
    mark: jest.fn(),
    measure: jest.fn(),
    clearMarks: jest.fn(),
    clearMeasures: jest.fn(),
  };
}

// Mock fetch if not available
if (typeof fetch === 'undefined') {
  global.fetch = jest.fn(() =>
    Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({}),
      text: () => Promise.resolve(''),
    }),
  );
}

// Clean up after each test
afterEach(() => {
  // Clear all mocks
  jest.clearAllMocks();

  // Reset any timers
  if (jest.isMockFunction(setTimeout)) {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  }

  // Clean up DOM if in jsdom environment
  if (typeof document !== 'undefined') {
    document.body.innerHTML = '';
  }
});
