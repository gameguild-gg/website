/**
 * Reflect bindings (Reflect namespace)
 */
export function createReflectBindings() {
  return {
    get: (target: object, propertyKey: PropertyKey): unknown => Reflect.get(target, propertyKey),
    has: (target: object, propertyKey: PropertyKey) => Reflect.has(target, propertyKey),
    set: (target: object, propertyKey: PropertyKey, value: unknown) =>
      Reflect.set(target, propertyKey, value),
  }
}
