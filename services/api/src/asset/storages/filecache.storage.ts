import { Storage } from '../storage';
import { Injectable, Logger } from '@nestjs/common';
import { ApiConfigService, SourceInfo } from '../../common/config.service';
import { rimraf } from 'rimraf';
import * as fs from 'node:fs';
import { AssetBase } from '../asset.base';

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
    fs.mkdirSync(this.assetCacheDir);
  }

  async cleanExpiredCache(folder: string = null): Promise<void> {
    const now = new Date().getTime();
    // recursively navigate through all files in the cache and delete the ones that are expired

    if (!folder) {
      folder = this.assetCacheDir;
    }

    // list all folders in the current folder
    const subfolders = fs.readdirSync(folder, { withFileTypes: true });
    for (const entry of subfolders) {
      if (entry.isDirectory()) {
        await this.cleanExpiredCache(`${folder}/${entry.name}`);
      } else {
        const stats = fs.statSync(`${folder}/${entry.name}`);
        if (stats.mtime.getTime() + this.ttl < now) {
          fs.unlink(`${folder}/${entry.name}`, (err) => {
            if (err) {
              this.logger.error(
                `Failed to delete file ${folder}/${entry.name}`,
              );
            }
          });
        }
      }
    }
  }

  store(file: Express.Multer.File) {}
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
