import { BeforeInsert, BeforeUpdate, Column, CreateDateColumn, DeleteDateColumn, PrimaryGeneratedColumn, UpdateDateColumn, VersionColumn } from 'typeorm';
import { EntityDto } from '@/common/dtos/entity.dto';
import * as crypto from 'crypto';

export abstract class EntityBase implements EntityDto {
  @PrimaryGeneratedColumn('uuid')
  public readonly id: string;

  @VersionColumn()
  public readonly version: number;

  @Column({ type: 'varchar', length: 64, nullable: false })
  public checksum: string;

  @CreateDateColumn({ type: 'timestamp' })
  public readonly createdAt: Date;

  @UpdateDateColumn({ type: 'timestamp' })
  public readonly updatedAt: Date;

  @DeleteDateColumn({ type: 'timestamp', nullable: true })
  public readonly deletedAt: Date;

  @BeforeInsert()
  @BeforeUpdate()
  public generateChecksum(): void {
    const collectProperties = (object: any): Record<string, any> => {
      if (!object || object === Object.prototype) return {};
      const ownProperties = Object.getOwnPropertyNames(object)
        .filter((key) => key !== 'constructor' && key !== 'checksum' && typeof object[key] !== 'function')
        .reduce((accumulator, key) => ({ ...accumulator, [key]: object[key] }), {} as Record<string, any>);
      return { ...collectProperties(Object.getPrototypeOf(object)), ...ownProperties };
    };

    const getAllProperties = (): Record<string, any> => collectProperties(this);

    const properties = getAllProperties();
    const sortedProperties = Object.keys(properties)
      .sort()
      .reduce((accumulator, key) => ({ ...accumulator, [key]: properties[key] }), {} as Record<string, any>);

    const data = JSON.stringify(sortedProperties);
    this.checksum = crypto.createHash('sha256').update(data).digest('hex');
  }
}
