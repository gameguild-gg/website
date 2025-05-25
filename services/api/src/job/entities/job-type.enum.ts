import { registerEnumType } from '@nestjs/graphql';

export enum JobTypeEnum {
  CONTINUOUS = 'CONTINUOUS',
  TASK = 'TASK',
}

registerEnumType(JobTypeEnum, {
  name: 'JobTypeEnum',
});
