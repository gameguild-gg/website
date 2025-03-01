import { MigrationInterface, QueryRunner } from "typeorm";

export class RemoveAuthorFromCourseEntity1736615633773 implements MigrationInterface {
    name = 'RemoveAuthorFromCourseEntity1736615633773'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "course" DROP CONSTRAINT "FK_907888662706db7def5705ef5d3"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "author_id"`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "course" ADD "author_id" uuid`);
        await queryRunner.query(`ALTER TABLE "course" ADD CONSTRAINT "FK_907888662706db7def5705ef5d3" FOREIGN KEY ("author_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
