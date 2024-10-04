import { MigrationInterface, QueryRunner } from "typeorm";

export class CheckingIfINeedAMigration1728072626817 implements MigrationInterface {
    name = 'CheckingIfINeedAMigration1728072626817'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TYPE "public"."job-post_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`CREATE TABLE "job-post" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "slug" character varying(255) NOT NULL, "title" character varying(1024) NOT NULL, "summary" character varying(1024) NOT NULL, "body" text NOT NULL, "visibility" "public"."job-post_visibility_enum" NOT NULL DEFAULT 'DRAFT', "thumbnail" character varying(1024) DEFAULT '', "owner_id" uuid, CONSTRAINT "PK_19c6a7e604f32663d470210610b" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_23668b4e7f7b6908f461bc5522" ON "job-post" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_244c4f211882ced89a917ec4f3" ON "job-post" ("title") `);
        await queryRunner.query(`CREATE INDEX "IDX_86431017ad9aaf29066c5f697c" ON "job-post" ("visibility") `);
        await queryRunner.query(`CREATE TABLE "job-tag" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "name" character varying(256) NOT NULL, "job_id" uuid, CONSTRAINT "PK_835586a09d10e323fdec92aaaa1" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_12d56147187c085b6bea313618" ON "job-tag" ("name") `);
        await queryRunner.query(`CREATE TABLE "job-post_editors_user" ("job-post_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_b2de4693be0a04ac6bae0e82544" PRIMARY KEY ("job-post_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_31aeb5812812256abf78ba261b" ON "job-post_editors_user" ("job-post_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_612fe7371eb62167d48add018f" ON "job-post_editors_user" ("user_id") `);
        await queryRunner.query(`ALTER TABLE "job-post" ADD CONSTRAINT "FK_33aeb92a17d4888a8f62336bb8e" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "job-tag" ADD CONSTRAINT "FK_281854bc897857ec3fe3412bef8" FOREIGN KEY ("job_id") REFERENCES "job-post"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "job-post_editors_user" ADD CONSTRAINT "FK_31aeb5812812256abf78ba261b5" FOREIGN KEY ("job-post_id") REFERENCES "job-post"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "job-post_editors_user" ADD CONSTRAINT "FK_612fe7371eb62167d48add018f4" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-post_editors_user" DROP CONSTRAINT "FK_612fe7371eb62167d48add018f4"`);
        await queryRunner.query(`ALTER TABLE "job-post_editors_user" DROP CONSTRAINT "FK_31aeb5812812256abf78ba261b5"`);
        await queryRunner.query(`ALTER TABLE "job-tag" DROP CONSTRAINT "FK_281854bc897857ec3fe3412bef8"`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP CONSTRAINT "FK_33aeb92a17d4888a8f62336bb8e"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_612fe7371eb62167d48add018f"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_31aeb5812812256abf78ba261b"`);
        await queryRunner.query(`DROP TABLE "job-post_editors_user"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_12d56147187c085b6bea313618"`);
        await queryRunner.query(`DROP TABLE "job-tag"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_86431017ad9aaf29066c5f697c"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_244c4f211882ced89a917ec4f3"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_23668b4e7f7b6908f461bc5522"`);
        await queryRunner.query(`DROP TABLE "job-post"`);
        await queryRunner.query(`DROP TYPE "public"."job-post_visibility_enum"`);
    }

}
