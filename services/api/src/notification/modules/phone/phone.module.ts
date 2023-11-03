import { Module } from '@nestjs/common';
import { PhoneService } from './phone.service';

@Module({
  providers: [PhoneService],
  exports: [PhoneService],
})
export class PhoneModule {}
