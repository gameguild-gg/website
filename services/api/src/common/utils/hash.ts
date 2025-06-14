import { ethers } from 'ethers';

/**
 * generate hash from password or string
 * @param {string} data
 * @returns {string}
 */
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
