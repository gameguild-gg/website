import { MigrationInterface, QueryRunner } from "typeorm";

export class AssetDropNonNullReference1732745017897 implements MigrationInterface {
    name = 'AssetDropNonNullReference1732745017897'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "images" ALTER COLUMN "references" DROP NOT NULL`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "images" ALTER COLUMN "references" SET NOT NULL`);
    }

}
