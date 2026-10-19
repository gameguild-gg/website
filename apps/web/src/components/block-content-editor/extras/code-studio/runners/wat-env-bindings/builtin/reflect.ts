/**
 * Reflect bindings (Reflect namespace)
 */
export function createReflectBindings() {
  return {
    get: (target: any, propertyKey: string) => Reflect.get(target, propertyKey),
    has: (target: any, propertyKey: string) => Reflect.has(target, propertyKey),
    set: (target: any, propertyKey: string, value: any) =>
      Reflect.set(target, propertyKey, value),
  }
}
