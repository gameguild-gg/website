export type Except<T, K extends keyof T | (keyof T)[]> = Pick<T, Exclude<keyof T, K extends (keyof T)[] ? K[number] : K>>;
