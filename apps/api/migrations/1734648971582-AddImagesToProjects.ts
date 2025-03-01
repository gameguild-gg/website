import { MigrationInterface, QueryRunner } from "typeorm";

export class AddImagesToProjects1734648971582 implements MigrationInterface {
    name = 'AddImagesToProjects1734648971582'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project" DROP CONSTRAINT "FK_2a091991ca5b2917a9be369912b"`);
        await queryRunner.query(`ALTER TABLE "project" ADD CONSTRAINT "FK_2a091991ca5b2917a9be369912b" FOREIGN KEY ("banner_id") REFERENCES "images"("id") ON DELETE CASCADE ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project" DROP CONSTRAINT "FK_2a091991ca5b2917a9be369912b"`);
        await queryRunner.query(`ALTER TABLE "project" ADD CONSTRAINT "FK_2a091991ca5b2917a9be369912b" FOREIGN KEY ("banner_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
