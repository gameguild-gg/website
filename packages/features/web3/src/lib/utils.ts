/** Chain id / message encoding helpers shared by the adapters and SIWE flow. */

/**
 * Normalize any chain id representation into the `0x`-prefixed hex form
 * EIP-1193 chain methods expect (`wallet_switchEthereumChain`, `eth_chainId`).
 */
export const formatChainId = (chainId: string | number | bigint): string => {
  if (typeof chainId === 'bigint') return `0x${chainId.toString(16)}`;

  if (typeof chainId === 'number') return `0x${chainId.toString(16)}`;

  return chainId.startsWith('0x') ? chainId : `0x${Number.parseInt(chainId, 10).toString(16)}`;
};

/**
 * Normalize any chain id representation into the decimal string the GameGuild
 * SIWE endpoints use (`"1"`, `"11155111"`, ...).
 */
export const parseChainId = (chainId: string | number | bigint): string => {
  if (typeof chainId === 'number') return String(chainId);

  if (typeof chainId === 'bigint') return chainId.toString();

  if (chainId.startsWith('0x')) return BigInt(chainId).toString(10);

  return String(Number.parseInt(chainId, 10));
};

/**
 * Hex-encode a UTF-8 message for `personal_sign`. Providers hex-decode the
 * payload before applying the EIP-191 prefix, so the signature recovered by
 * the backend (`EncodeUTF8AndEcRecover`) matches the original message text.
 */
export const utf8ToHex = (message: string): string => {
  const bytes = new TextEncoder().encode(message);
  let hex = '0x';
  for (const byte of bytes) {
    hex += byte.toString(16).padStart(2, '0');
  }
  return hex;
};

/** Compact display form of an address: `0x1234…abcd`. */
export const shortenAddress = (address: string): string => {
  if (!/^0x[0-9a-fA-F]{40}$/.test(address)) return address;
  return `${address.slice(0, 6)}…${address.slice(-4)}`;
};

/** Validate the `0x` + 40 hex-char shape used by the SIWE endpoints. */
export const isEthereumAddress = (value: string): boolean => /^0x[0-9a-fA-F]{40}$/.test(value);
