import { Entity, Column, ManyToOne, Index, DeleteDateColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsNotEmpty, IsOptional, IsNumber, IsDate, IsJSON, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { EntityBase } from '../../common/entities/entity.base';
import { UserCertificate } from './user-certificate.entity';

@Entity('certificate_blockchain_anchors')
@Index((entity) => [entity.certificate])
@Index((entity) => [entity.transactionHash])
@Index((entity) => [entity.blockchainNetwork])
@Index((entity) => [entity.tokenId])
export class CertificateBlockchainAnchor extends EntityBase {
  @ApiProperty({ type: () => UserCertificate, description: 'Reference to the certificate being anchored' })
  @ManyToOne(() => UserCertificate, { nullable: false })
  @ValidateNested()
  @Type(() => UserCertificate)
  certificate: UserCertificate;

  @Column({ type: 'varchar' })
  @ApiProperty({ description: 'Name or identifier of the blockchain network used (ethereum, polygon, binance, etc.)' })
  @IsNotEmpty()
  @IsString()
  blockchainNetwork: string;

  @Column({ type: 'varchar' })
  @ApiProperty({ description: 'Transaction hash where the certificate was anchored' })
  @IsNotEmpty()
  @IsString()
  transactionHash: string;

  @Column({ type: 'bigint' })
  @ApiProperty({ description: 'Block number containing the transaction' })
  @IsNotEmpty()
  @IsNumber()
  blockNumber: number;

  @Column({ type: 'varchar', nullable: true })
  @ApiProperty({ description: 'Address of contract used for verification, if applicable', required: false })
  @IsOptional()
  @IsString()
  smartContractAddress: string | null;

  @Column({ type: 'varchar', nullable: true })
  @ApiProperty({ description: 'ID of NFT if certificate is represented as an NFT', required: false })
  @IsOptional()
  @IsString()
  tokenId: string | null;

  @Column({ type: 'timestamp' })
  @ApiProperty({ description: 'Timestamp when the anchor was confirmed on the blockchain' })
  @IsNotEmpty()
  @IsDate()
  anchoredAt: Date;

  @Column({ type: 'varchar', nullable: true })
  @ApiProperty({ description: 'URL where anyone can verify this certificate on a block explorer', required: false })
  @IsOptional()
  @IsString()
  publicVerificationUrl: string | null;

  @Column({ type: 'varchar', nullable: true })
  @ApiProperty({ description: 'IPFS hash if certificate data is stored on IPFS', required: false })
  @IsOptional()
  @IsString()
  ipfsHash: string | null;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Additional blockchain-specific data related to this anchor', required: false })
  @IsOptional()
  @IsJSON()
  metadata: object | null;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the blockchain anchor was deleted, null if active', required: false })
  deletedAt: Date | null;
}
