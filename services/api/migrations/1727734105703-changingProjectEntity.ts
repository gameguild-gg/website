import { MigrationInterface, QueryRunner } from "typeorm";

export class ChangingProjectEntity1727734105703 implements MigrationInterface {
    name = 'ChangingProjectEntity1727734105703'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "description"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "project" ADD "title" character varying(1024) NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON "project" ("title") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "project" ADD "title" character varying(255) NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON "project" ("title") `);
        await queryRunner.query(`ALTER TABLE "project" ADD "description" character varying(255) NOT NULL`);
    }

}
