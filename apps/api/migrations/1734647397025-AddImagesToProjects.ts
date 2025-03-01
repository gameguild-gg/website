import { MigrationInterface, QueryRunner } from "typeorm";

export class AddImagesToProjects1734647397025 implements MigrationInterface {
    name = 'AddImagesToProjects1734647397025'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "project_screenshots_images" ("project_id" uuid NOT NULL, "images_id" uuid NOT NULL, CONSTRAINT "PK_2e301330d17fb5c8f1695a51e39" PRIMARY KEY ("project_id", "images_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_4e1f8d02befe1e5a392910b624" ON "project_screenshots_images" ("project_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_ef0ec5b9da603747097dea89a4" ON "project_screenshots_images" ("images_id") `);
        await queryRunner.query(`ALTER TABLE "project_screenshots_images" ADD CONSTRAINT "FK_4e1f8d02befe1e5a392910b624b" FOREIGN KEY ("project_id") REFERENCES "project"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "project_screenshots_images" ADD CONSTRAINT "FK_ef0ec5b9da603747097dea89a41" FOREIGN KEY ("images_id") REFERENCES "images"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project_screenshots_images" DROP CONSTRAINT "FK_ef0ec5b9da603747097dea89a41"`);
        await queryRunner.query(`ALTER TABLE "project_screenshots_images" DROP CONSTRAINT "FK_4e1f8d02befe1e5a392910b624b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_ef0ec5b9da603747097dea89a4"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4e1f8d02befe1e5a392910b624"`);
        await queryRunner.query(`DROP TABLE "project_screenshots_images"`);
    }

}
