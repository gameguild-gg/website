import { MigrationInterface, QueryRunner } from "typeorm";

export class AddThumbnailToImageEntity1733879445555 implements MigrationInterface {
    name = 'AddThumbnailToImageEntity1733879445555'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "post" RENAME COLUMN "thumbnail" TO "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "lecture" RENAME COLUMN "thumbnail" TO "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "chapter" RENAME COLUMN "thumbnail" TO "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "course" RENAME COLUMN "thumbnail" TO "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" RENAME COLUMN "thumbnail" TO "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "job-post" RENAME COLUMN "thumbnail" TO "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "project" RENAME COLUMN "thumbnail" TO "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "post" ADD "thumbnail_id" uuid`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD "thumbnail_id" uuid`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "thumbnail_id" uuid`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "course" ADD "thumbnail_id" uuid`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "thumbnail_id" uuid`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "thumbnail_id" uuid`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "project" ADD "thumbnail_id" uuid`);
        await queryRunner.query(`ALTER TABLE "post" ADD CONSTRAINT "FK_f1326fde621b704a33759e54fe5" FOREIGN KEY ("thumbnail_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_08bec6f2e24762d264e0a1e29d8" FOREIGN KEY ("thumbnail_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD CONSTRAINT "FK_d25efc1a13c3a927f5846d0a149" FOREIGN KEY ("thumbnail_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "course" ADD CONSTRAINT "FK_39231fcb10d7a7ff129ce7e51fc" FOREIGN KEY ("thumbnail_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_d94403fe0897f63261fe88b9afe" FOREIGN KEY ("thumbnail_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD CONSTRAINT "FK_af3a1de2186046eb06e9aeb5809" FOREIGN KEY ("thumbnail_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "project" ADD CONSTRAINT "FK_a9fe4081c01805449159cbf96a1" FOREIGN KEY ("thumbnail_id") REFERENCES "images"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project" DROP CONSTRAINT "FK_a9fe4081c01805449159cbf96a1"`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP CONSTRAINT "FK_af3a1de2186046eb06e9aeb5809"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_d94403fe0897f63261fe88b9afe"`);
        await queryRunner.query(`ALTER TABLE "course" DROP CONSTRAINT "FK_39231fcb10d7a7ff129ce7e51fc"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP CONSTRAINT "FK_d25efc1a13c3a927f5846d0a149"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_08bec6f2e24762d264e0a1e29d8"`);
        await queryRunner.query(`ALTER TABLE "post" DROP CONSTRAINT "FK_f1326fde621b704a33759e54fe5"`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "project" ADD "thumbnail_id" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "thumbnail_id" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "thumbnail_id" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "course" ADD "thumbnail_id" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "thumbnail_id" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD "thumbnail_id" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "thumbnail_id"`);
        await queryRunner.query(`ALTER TABLE "post" ADD "thumbnail_id" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "project" RENAME COLUMN "thumbnail_id" TO "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "job-post" RENAME COLUMN "thumbnail_id" TO "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "proposal" RENAME COLUMN "thumbnail_id" TO "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "course" RENAME COLUMN "thumbnail_id" TO "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "chapter" RENAME COLUMN "thumbnail_id" TO "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "lecture" RENAME COLUMN "thumbnail_id" TO "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "post" RENAME COLUMN "thumbnail_id" TO "thumbnail"`);
    }

}
