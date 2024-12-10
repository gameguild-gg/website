import { MigrationInterface, QueryRunner } from "typeorm";

export class FixAssetTypes1732911933554 implements MigrationInterface {
    name = 'FixAssetTypes1732911933554'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user_profile" RENAME COLUMN "picture" TO "picture_id"`);
        await queryRunner.query(`ALTER TABLE "images" DROP COLUMN "references"`);
        await queryRunner.query(`ALTER TABLE "images" ALTER COLUMN "width" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "images" ALTER COLUMN "height" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "picture_id"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "picture_id" uuid`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD CONSTRAINT "UQ_e4238c3828bc51ff8ca27c46385" UNIQUE ("picture_id")`);
        await queryRunner.query(`CREATE INDEX "IDX_b27820f9c4eb00f2afc4e5b616" ON "images" ("path") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "asset_source_path_filename_idx" ON "images" ("source", "path", "filename") `);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD CONSTRAINT "FK_e4238c3828bc51ff8ca27c46385" FOREIGN KEY ("picture_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user_profile" DROP CONSTRAINT "FK_e4238c3828bc51ff8ca27c46385"`);
        await queryRunner.query(`DROP INDEX "public"."asset_source_path_filename_idx"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_b27820f9c4eb00f2afc4e5b616"`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP CONSTRAINT "UQ_e4238c3828bc51ff8ca27c46385"`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "picture_id"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "picture_id" character varying(256)`);
        await queryRunner.query(`ALTER TABLE "images" ALTER COLUMN "height" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "images" ALTER COLUMN "width" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "images" ADD "references" jsonb`);
        await queryRunner.query(`ALTER TABLE "user_profile" RENAME COLUMN "picture_id" TO "picture"`);
    }

}
