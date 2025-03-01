import { Injectable } from '@nestjs/common';
import sharp from 'sharp';
import { FileCacheStorageService } from './storages/filecache.storage';

export type CompressImageServiceConfig = {
  width: number;
  height?: number;
  quality?: number;
  fit?: 'cover' | 'contain' | 'fill' | 'inside' | 'outside';
};

@Injectable()
export class CompressImageService {
  constructor(private filecache: FileCacheStorageService) {}
  async compressJpeg(
    filePath: string,
    config: Partial<CompressImageServiceConfig>,
  ) {
    const { width = null, height = null, quality = 70, fit = 'cover' } = config;
    let compressedFilename: string;
    if (!height && !width) {
      compressedFilename = `${filePath}-${quality}-${fit}.jpeg`;
    } else if (height)
      compressedFilename = `${filePath}-${quality}-${width}x${height}-${fit}.jpeg`;
    else compressedFilename = `${filePath}-${quality}-${width}px-${fit}.jpeg`;
    await sharp(filePath)
      .resize({ width, height, fit })
      .jpeg({ quality })
      .toFile(compressedFilename);

    return compressedFilename;
  }

  async compressPng(
    filePath: string,
    config: Partial<CompressImageServiceConfig>,
  ) {
    const { width = null, height = null, quality = 70, fit = 'cover' } = config;
    let compressedFilename: string;
    if (!height && !width) {
      compressedFilename = `${filePath}-${quality}-${fit}.png`;
    } else if (height)
      compressedFilename = `${filePath}-${quality}-${width}x${height}-${fit}.png`;
    else compressedFilename = `${filePath}-${quality}-${width}px-${fit}.png`;
    await sharp(filePath)
      .resize({ width, height, fit })
      .png({ quality })
      .toFile(compressedFilename);

    return compressedFilename;
  }
}
