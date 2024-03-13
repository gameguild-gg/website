import { Module } from '@nestjs/common';
import { IpfsService } from './ipfs.service';
import { TypeOrmModule } from "@nestjs/typeorm";
import { IpfsEntity } from "./ipfs.entity";

@Module({
  imports: [TypeOrmModule.forFeature([IpfsEntity])],
  providers: [IpfsService],
  exports: [IpfsService]
})
export class IpfsModule {}
