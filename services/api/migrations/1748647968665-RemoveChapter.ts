import { MigrationInterface, QueryRunner } from "typeorm";

export class RemoveChapter1748647968665 implements MigrationInterface {
    name = 'RemoveChapter1748647968665'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "chapter_id"`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "lecture" ADD "chapter_id" uuid`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab" FOREIGN KEY ("chapter_id") REFERENCES "chapter"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
