export type Except<T, K extends keyof T | (keyof T)[]> = Pick<T, Exclude<keyof T, K extends (keyof T)[] ? K[number] : K>>;
export type Constructor<T = any> = new (...args: any[]) => T;
// Define um tipo para construtores abstratos
export type AbstractConstructor<T = any> = abstract new (...args: any[]) => T;
