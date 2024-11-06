import { AssetBase } from './asset.base';
import { OkDto } from '../common/dtos/ok.dto';

export abstract class Storage {
  abstract store(file: Express.Multer.File): Promise<AssetBase>;
  abstract delete(): Promise<OkDto>;
  abstract get(asset: Partial<AssetBase>): Promise<AssetBase>;
}
