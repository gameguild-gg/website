import {ethers} from "ethers";

/**
 * generate hash from password or string
 * @param {string} data
 * @returns {string}
 */
export function generateHash(data: string, salt: string = ""): string {
    return ethers.keccak256(ethers.toUtf8Bytes(data+salt));
}

export function generateRandomSalt(): string {
    return ethers.hexlify(ethers.randomBytes(32));
}

/**
 * validate text with hash
 * @param {string} password
 * @param {string} hash
 * @returns {Promise<boolean>}
 */
export function validateHash(
    password: string | undefined,
    hash: string | undefined,
    salt: string = "",
): boolean {
    if (!password || !hash) {
        return false;
    }
    return generateHash(password, salt) === hash;
}