import { MigrationInterface, QueryRunner } from "typeorm";

export class RenameGameToProject1726243379117 implements MigrationInterface {
    name = 'RenameGameToProject1726243379117'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TYPE "public"."project_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`CREATE TABLE "project" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "slug" character varying(255) NOT NULL, "title" character varying(1024) NOT NULL, "summary" character varying(1024) NOT NULL, "body" text NOT NULL, "visibility" "public"."project_visibility_enum" NOT NULL DEFAULT 'DRAFT', "thumbnail" character varying(1024) DEFAULT '', "owner_id" uuid NOT NULL, CONSTRAINT "PK_4d68b1358bb5b766d3e78f32f57" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_6fce32ddd71197807027be6ad3" ON "project" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON "project" ("title") `);
        await queryRunner.query(`CREATE INDEX "IDX_0c2943d87d6b4f22558a7fce71" ON "project" ("visibility") `);
        await queryRunner.query(`CREATE TABLE "project_version" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "version" character varying(64) NOT NULL, "archive_url" character varying(1024) NOT NULL, "notes_url" character varying(1024) NOT NULL, "feedback_form" jsonb NOT NULL, "feedback_deadline" TIMESTAMP WITH TIME ZONE NOT NULL, "game_id" uuid, CONSTRAINT "UQ_4aa15ae20ae619ff1562db73861" UNIQUE ("version", "game_id"), CONSTRAINT "PK_249c24f66d8f7e8ea6f9ff462fb" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_4b6747e098d96a0e805692810d" ON "project_version" ("feedback_deadline") `);
        await queryRunner.query(`CREATE TABLE "project_feedback_response" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "responses" jsonb NOT NULL, "version_id" uuid, "user_id" uuid, CONSTRAINT "PK_8ecbac3eb61113faffe1166e11e" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "project_editors_user" ("project_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_efd60535d94affeccba7365e591" PRIMARY KEY ("project_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_4efce1aaecf2b3d2523959a501" ON "project_editors_user" ("project_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_8f5fba1aed12561d33ab675a24" ON "project_editors_user" ("user_id") `);
        await queryRunner.query(`ALTER TABLE "project" ADD CONSTRAINT "FK_d40afe32d1d771bea7a5f468185" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "project_version" ADD CONSTRAINT "FK_f5931748d5514ab10ade223d431" FOREIGN KEY ("game_id") REFERENCES "project"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "project_feedback_response" ADD CONSTRAINT "FK_b321f272db3baa8decf94446c16" FOREIGN KEY ("version_id") REFERENCES "project_version"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "project_feedback_response" ADD CONSTRAINT "FK_d01ed37f3d5ac13bca59ba2956e" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "project_editors_user" ADD CONSTRAINT "FK_4efce1aaecf2b3d2523959a5019" FOREIGN KEY ("project_id") REFERENCES "project"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "project_editors_user" ADD CONSTRAINT "FK_8f5fba1aed12561d33ab675a24b" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project_editors_user" DROP CONSTRAINT "FK_8f5fba1aed12561d33ab675a24b"`);
        await queryRunner.query(`ALTER TABLE "project_editors_user" DROP CONSTRAINT "FK_4efce1aaecf2b3d2523959a5019"`);
        await queryRunner.query(`ALTER TABLE "project_feedback_response" DROP CONSTRAINT "FK_d01ed37f3d5ac13bca59ba2956e"`);
        await queryRunner.query(`ALTER TABLE "project_feedback_response" DROP CONSTRAINT "FK_b321f272db3baa8decf94446c16"`);
        await queryRunner.query(`ALTER TABLE "project_version" DROP CONSTRAINT "FK_f5931748d5514ab10ade223d431"`);
        await queryRunner.query(`ALTER TABLE "project" DROP CONSTRAINT "FK_d40afe32d1d771bea7a5f468185"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_8f5fba1aed12561d33ab675a24"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4efce1aaecf2b3d2523959a501"`);
        await queryRunner.query(`DROP TABLE "project_editors_user"`);
        await queryRunner.query(`DROP TABLE "project_feedback_response"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4b6747e098d96a0e805692810d"`);
        await queryRunner.query(`DROP TABLE "project_version"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_0c2943d87d6b4f22558a7fce71"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_6fce32ddd71197807027be6ad3"`);
        await queryRunner.query(`DROP TABLE "project"`);
        await queryRunner.query(`DROP TYPE "public"."project_visibility_enum"`);
    }

}
