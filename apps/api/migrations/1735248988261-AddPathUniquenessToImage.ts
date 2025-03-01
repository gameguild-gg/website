import { MigrationInterface, QueryRunner } from "typeorm";

export class AddPathUniquenessToImage1735248988261 implements MigrationInterface {
    name = 'AddPathUniquenessToImage1735248988261'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."pathUniqueness"`);
        await queryRunner.query(`DROP INDEX "public"."asset_source_path_filename_idx"`);
        await queryRunner.query(`CREATE UNIQUE INDEX "pathUniqueness" ON "images" ("path", "filename", "source") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."pathUniqueness"`);
        await queryRunner.query(`CREATE UNIQUE INDEX "asset_source_path_filename_idx" ON "images" ("source", "path", "filename") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "pathUniqueness" ON "images" ("source", "path") `);
    }

}
