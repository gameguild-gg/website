// import { Injectable } from '@nestjs/common';
// import { NFTStorage, File, Blob } from 'nft.storage'
// import { TypeOrmCrudService } from "@dataui/crud-typeorm";
// import { IpfsEntity } from "./ipfs.entity";
// import client from "@sendgrid/client";
// import { InjectRepository } from "@nestjs/typeorm";
// import { Repository } from "typeorm";
// import { FindOptionsWhere } from "typeorm/find-options/FindOptionsWhere";
//
// @Injectable()
// export class IpfsService extends TypeOrmCrudService<IpfsEntity> {
//   private client: NFTStorage;
//   constructor(@InjectRepository(IpfsEntity) private readonly repository: Repository<IpfsEntity>) {
//     super(repository);
//     this.client = new NFTStorage({ token: process.env.IPFS_KEY });
//   }
//   async upload(data: { blob: Blob, mimeType: string, fileName: string}): Promise<IpfsEntity> {
//       const cid = await this.client.storeBlob(data.blob);
//       let entity = this.repository.create({ CID: cid, mimeType: data.mimeType });
//       await this.repository.save(entity);
//       return entity;
//   }
// }
