import { MigrationInterface, QueryRunner } from "typeorm";

export class AddImagesToProjects1734645854258 implements MigrationInterface {
    name = 'AddImagesToProjects1734645854258'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project" ADD "banner_id" uuid`);
        await queryRunner.query(`ALTER TABLE "project" ADD CONSTRAINT "FK_2a091991ca5b2917a9be369912b" FOREIGN KEY ("banner_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project" DROP CONSTRAINT "FK_2a091991ca5b2917a9be369912b"`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "banner_id"`);
    }

}
