import { MigrationInterface, QueryRunner } from "typeorm";

export class FixLectureRelationTypes1736633954990 implements MigrationInterface {
    name = 'FixLectureRelationTypes1736633954990'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab"`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab" FOREIGN KEY ("chapter_id") REFERENCES "chapter"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab"`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab" FOREIGN KEY ("chapter_id") REFERENCES "course"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
