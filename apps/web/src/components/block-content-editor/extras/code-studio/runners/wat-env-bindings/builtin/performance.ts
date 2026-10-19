/**
 * Performance bindings (performance namespace)
 */
export function createPerformanceBindings() {
  return {
    now: () => performance.now(),
  }
}
