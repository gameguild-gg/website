import { MigrationInterface, QueryRunner } from "typeorm";

export class AddOriginalFilenameToAssetBase1735247679553 implements MigrationInterface {
    name = 'AddOriginalFilenameToAssetBase1735247679553'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "images" ADD "original_filename" character varying NOT NULL DEFAULT ''`);
        await queryRunner.query(`CREATE INDEX "IDX_382daa5c2a55e77f5a960012ac" ON "images" ("original_filename") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_382daa5c2a55e77f5a960012ac"`);
        await queryRunner.query(`ALTER TABLE "images" DROP COLUMN "original_filename"`);
    }

}
