import { readStr } from './shared';

export class Memory {
  memory: WebAssembly.Memory;
  buffer: ArrayBuffer;
  u8: Uint8Array;
  u32: Uint32Array;

  constructor(memory: WebAssembly.Memory) {
    this.memory = memory;
    this.buffer = this.memory.buffer;
    this.u8 = new Uint8Array(this.buffer);
    this.u32 = new Uint32Array(this.buffer);
  }

  check(): void {
    if (this.buffer.byteLength === 0) {
      this.buffer = this.memory.buffer;
      this.u8 = new Uint8Array(this.buffer);
      this.u32 = new Uint32Array(this.buffer);
    }
  }

  read8(o: number): number {
    return this.u8[o];
  }

  read32(o: number): number {
    return this.u32[o >> 2];
  }

  write8(o: number, v: number): void {
    this.u8[o] = v;
  }

  write32(o: number, v: number): void {
    this.u32[o >> 2] = v;
  }

  write64(o: number, vlo: number, vhi: number = 0): void {
    this.write32(o, vlo);
    this.write32(o + 4, vhi);
  }

  readStr(o: number, len: number): string {
    return readStr(this.u8, o, len);
  }

  // Null-terminated string.
  writeStr(o: number, str: string): number {
    o += this.write(o, str);
    this.write8(o, 0);
    return str.length + 1;
  }

  write(o: number, buf: ArrayBuffer | string | ArrayLike<number>): number {
    if (buf instanceof ArrayBuffer) {
      return this.write(o, new Uint8Array(buf));
    } else if (typeof buf === 'string') {
      return this.write(
        o,
        buf.split('').map((x) => x.charCodeAt(0)),
      );
    } else {
      const dst = new Uint8Array(this.buffer, o, buf.length);
      dst.set(buf as Uint8Array);
      return buf.length;
    }
  }
}
