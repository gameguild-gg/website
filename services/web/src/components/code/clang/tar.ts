import { assert, readStr } from './shared';
import { MemFS } from './memfs';

interface TarEntry {
  filename: string;
  mode: number;
  owner: number;
  group: number;
  size: number;
  mtim: number;
  checksum: number;
  type: string;
  linkname: string;
  ownerName: string;
  groupName: string;
  devMajor: string;
  devMinor: string;
  filenamePrefix: string;
  contents?: Uint8Array;
}

export class Tar {
  private u8: Uint8Array;
  private offset: number;

  constructor(buffer: ArrayBuffer) {
    this.u8 = new Uint8Array(buffer);
    this.offset = 0;
  }

  private readStr(len: number): string {
    const result = readStr(this.u8, this.offset, len);
    this.offset += len;
    return result;
  }

  private readOctal(len: number): number {
    return parseInt(this.readStr(len), 8);
  }

  private alignUp(): void {
    this.offset = (this.offset + 511) & ~511;
  }

  readEntry(): TarEntry | null {
    if (this.offset + 512 > this.u8.length) {
      return null;
    }

    const entry: TarEntry = {
      filename: this.readStr(100),
      mode: this.readOctal(8),
      owner: this.readOctal(8),
      group: this.readOctal(8),
      size: this.readOctal(12),
      mtim: this.readOctal(12),
      checksum: this.readOctal(8),
      type: this.readStr(1),
      linkname: this.readStr(100),
      ownerName: '',
      groupName: '',
      devMajor: '',
      devMinor: '',
      filenamePrefix: ''
    };

    if (this.readStr(8) !== 'ustar  ') {
      return null;
    }

    entry.ownerName = this.readStr(32);
    entry.groupName = this.readStr(32);
    entry.devMajor = this.readStr(8);
    entry.devMinor = this.readStr(8);
    entry.filenamePrefix = this.readStr(155);
    this.alignUp();

    if (entry.type === '0') {
      // Regular file.
      entry.contents = this.u8.subarray(this.offset, this.offset + entry.size);
      this.offset += entry.size;
      this.alignUp();
    } else if (entry.type !== '5') {
      // Directory.
      console.log('type', entry.type);
      assert(false);
    }
    return entry;
  }

  untar(memfs: MemFS): void {
    let entry: TarEntry | null;
    while ((entry = this.readEntry())) {
      switch (entry.type) {
        case '0': // Regular file.
          memfs.addFile(entry.filename, entry.contents!);
          break;
        case '5': // Directory
          memfs.addDirectory(entry.filename);
          break;
      }
    }
  }
}
