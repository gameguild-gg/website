import { Storage } from '../storage';
import { Injectable, Logger } from '@nestjs/common';
import { ApiConfigService, SourceInfo } from '../../common/config.service';
import { rimraf } from 'rimraf';
import * as fs from 'fs';
import { AssetBase } from '../asset.base';
const fsp = fs.promises;

export class AssetOnDisk {
  path: string;
  hash: string;
}

@Injectable()
export class FileCacheStorageService implements Storage {
  private readonly logger = new Logger(FileCacheStorageService.name);
  private assetSourceMap: Map<string, SourceInfo>;
  private assetCacheDir: string;
  private ttl: number;
  private maxCacheSize: number;
  private lastTimeCleaned: number;
  private fetchCounter: number;

  constructor(config: ApiConfigService) {
    this.assetCacheDir = config.assetCacheDir;
    this.maxCacheSize = config.assetCacheSize;
    this.ttl = config.assetCacheTTL;
    // this.cleanAllCache();
  }

  async cleanCacheForced(): Promise<void> {
    await rimraf(this.assetCacheDir);
    await fsp.mkdir(this.assetCacheDir);
  }

  async cleanExpiredCache(folder: string = null): Promise<void> {
    const now = new Date().getTime();
    // recursively navigate through all files in the cache and delete the ones that are expired

    if (!folder) {
      folder = this.assetCacheDir;
    }

    // list all folders in the current folder
    const subfolders = await fsp.readdir(folder, { withFileTypes: true });
    for (const entry of subfolders) {
      if (entry.isDirectory()) {
        await this.cleanExpiredCache(`${folder}/${entry.name}`);
      } else {
        const stats = fs.statSync(`${folder}/${entry.name}`);
        if (stats.mtime.getTime() + this.ttl < now) {
          await fsp.unlink(`${folder}/${entry.name}`);
        }
      }
    }
  }

  async store(file: Express.Multer.File): Promise<AssetOnDisk> {
    const { hashFile } = await import('hasha');

    const hash = await hashFile(file.path, { algorithm: 'sha256' });
    // split the hash into 2 parts to create a folder structure
    const outerFolder = hash.substring(0, 2);
    const innerFolder = hash.substring(2, 4);
    const targetFolder = `${this.assetCacheDir}/${outerFolder}/${innerFolder}`;
    const filename = `${hash}-${file.originalname}`;
    const targetPath = `${targetFolder}/${filename}`;

    // stores the file in the cache if it does not exist. async is used
    if (!fs.existsSync(targetPath)) {
      // create the target folder if it does not exist. async is used
      await fsp.mkdir(targetFolder, { recursive: true });
      // move the file to the target folder
      await fsp.rename(file.path, targetPath);
      return { path: targetPath, hash: hash };
    } else {
      // touch the file to update the mtime
      await fsp.utimes(targetPath, new Date(), new Date());
      // delete the temp file
      await fsp.unlink(file.path);
      return { path: targetPath, hash: hash };
    }
  }
  delete() {}
  get(asset: Partial<AssetBase>) {}

  // store(file: Express.Multer.File) {
  //   const nowms = new Date().getTime();
  //   // const tempFilePath = path.join(this.assetCacheDir, file.originalname);
  //
  //   const path = `${this.tempFolder}/${nowms}${file.originalname}`;
  // }
  // delete(): Promise<OkDto> {
  //   throw new Error('Method not implemented.');
  // }
  // get(asset): Promise<AssetBase> {
  //   throw new Error('Method not implemented.');
  // }
}
