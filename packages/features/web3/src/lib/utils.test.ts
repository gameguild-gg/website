import { describe, expect, it } from 'vitest';
import { formatChainId, isEthereumAddress, parseChainId, shortenAddress, utf8ToHex } from './utils';

describe('formatChainId', () => {
  it('converts decimal strings to hex', () => {
    expect(formatChainId('1')).toBe('0x1');
    expect(formatChainId('11155111')).toBe('0xaa36a7');
  });

  it('passes hex through unchanged', () => {
    expect(formatChainId('0xaa36a7')).toBe('0xaa36a7');
  });

  it('converts numbers and bigints', () => {
    expect(formatChainId(1)).toBe('0x1');
    expect(formatChainId(1337n)).toBe('0x539');
  });
});

describe('parseChainId', () => {
  it('converts hex chain ids to decimal strings', () => {
    expect(parseChainId('0x1')).toBe('1');
    expect(parseChainId('0xaa36a7')).toBe('11155111');
  });

  it('passes decimal strings and numbers through', () => {
    expect(parseChainId('1')).toBe('1');
    expect(parseChainId(61)).toBe('61');
    expect(parseChainId(61n)).toBe('61');
  });
});

describe('utf8ToHex', () => {
  it('hex-encodes UTF-8 bytes for personal_sign', () => {
    expect(utf8ToHex('a')).toBe('0x61');
    expect(utf8ToHex('GameGuild')).toBe('0x47616d654775696c64');
    expect(utf8ToHex('é')).toBe('0xc3a9');
  });

  it('round-trips through TextDecoder', () => {
    const message = 'Service: GameGuild\nNonce: 8f3e';
    const decoded = new TextDecoder().decode(
      Uint8Array.from(utf8ToHex(message).slice(2).match(/.{2}/g)!.map((byte) => Number.parseInt(byte, 16))),
    );
    expect(decoded).toBe(message);
  });
});

describe('shortenAddress', () => {
  it('shortens checksummed addresses', () => {
    expect(shortenAddress('0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb0')).toBe('0x742d…bEb0');
  });

  it('returns invalid values unchanged', () => {
    expect(shortenAddress('not-an-address')).toBe('not-an-address');
  });
});

describe('isEthereumAddress', () => {
  it('accepts 20-byte hex addresses', () => {
    expect(isEthereumAddress('0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb0')).toBe(true);
  });

  it('rejects malformed shapes', () => {
    expect(isEthereumAddress('742d35Cc6634C0532925a3b844Bc9e7595f0bEb0')).toBe(false);
    expect(isEthereumAddress('0x742d')).toBe(false);
    expect(isEthereumAddress('')).toBe(false);
  });
});
