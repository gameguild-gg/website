import { ethers } from 'ethers';

export function generateHash(data: string, salt?: string): string {
  if (!salt) salt = '';

  return ethers.id(data + salt);
}

export function validateHash(data: string, hash: string, salt?: string): boolean {
  return generateHash(data, salt) === hash;
}

export function generateRandomSalt(): string {
  return ethers.hexlify(ethers.randomBytes(32));
}

export type Algorithm = 'HS256' | 'HS384' | 'HS512' | 'RS256' | 'RS384' | 'RS512' | 'ES256' | 'ES384' | 'ES512' | 'PS256' | 'PS384' | 'PS512' | 'none';

const ALGORITHMS: Algorithm[] = ['HS256', 'HS384', 'HS512', 'RS256', 'RS384', 'RS512', 'ES256', 'ES384', 'ES512', 'PS256', 'PS384', 'PS512', 'none'];

export function isAlgorithm(value: string): value is Algorithm {
  return ALGORITHMS.includes(value as Algorithm);
}

export function parseAlgorithm(value: string): Algorithm {
  if (!isAlgorithm(value)) {
    throw new Error(`Invalid value for algorithm: ${value}`);
  }
  return value;
}
