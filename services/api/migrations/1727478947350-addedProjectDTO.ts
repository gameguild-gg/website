import { MigrationInterface, QueryRunner } from "typeorm";

export class AddedProjectDTO1727478947350 implements MigrationInterface {
    name = 'AddedProjectDTO1727478947350'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "ticket" DROP COLUMN "owner_username"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "project" ADD "title" character varying(255) NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON "project" ("title") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "project" ADD "title" character varying(1024) NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON "project" ("title") `);
        await queryRunner.query(`ALTER TABLE "ticket" ADD "owner_username" text`);
    }

}
