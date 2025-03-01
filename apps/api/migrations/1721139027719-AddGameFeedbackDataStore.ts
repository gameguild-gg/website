import { MigrationInterface, QueryRunner } from "typeorm";

export class AddGameFeedbackDataStore1721139027719 implements MigrationInterface {
    name = 'AddGameFeedbackDataStore1721139027719'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "game_feedback_response" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "version_id" uuid, "user_id" uuid, CONSTRAINT "PK_06ba706538a5547bd21671dc818" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "game_version" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "version" character varying NOT NULL, "archive_url" character varying NOT NULL, "notes_url" character varying NOT NULL, "feedback_form" character varying NOT NULL, "feedback_deadline" TIMESTAMP WITH TIME ZONE NOT NULL, "game_id" uuid, CONSTRAINT "PK_272f36b21fb4f0c43edd12fcfbe" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TYPE "public"."game_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`CREATE TABLE "game" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "slug" character varying(255) DEFAULT '', "title" character varying(1024) DEFAULT '', "summary" character varying(1024) DEFAULT '', "body" jsonb DEFAULT '{}', "visibility" "public"."game_visibility_enum" NOT NULL DEFAULT 'DRAFT', "thumbnail" character varying DEFAULT '', "owner_id" uuid, CONSTRAINT "PK_352a30652cd352f552fef73dec5" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_8ea221926a26ee68a25c904b65" ON "game" ("visibility") `);
        await queryRunner.query(`CREATE TABLE "game_team_user" ("game_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_202cf19a556a132e1dff266b7ad" PRIMARY KEY ("game_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_dea2174a735e6547fa42537015" ON "game_team_user" ("game_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_359197ec4d167804a84586689b" ON "game_team_user" ("user_id") `);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."post_type_enum"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."lecture_type_enum"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."chapter_type_enum"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."course_type_enum"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."proposal_type_enum"`);
        await queryRunner.query(`ALTER TABLE "game_feedback_response" ADD CONSTRAINT "FK_7e65414778ce14d3b69c7392db4" FOREIGN KEY ("version_id") REFERENCES "game_version"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "game_feedback_response" ADD CONSTRAINT "FK_b27f98ad277a0e2418f4dddbe5a" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "game_version" ADD CONSTRAINT "FK_5a4e407c2898e29b00136632b33" FOREIGN KEY ("game_id") REFERENCES "game"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "game" ADD CONSTRAINT "FK_678fcc30dbaf1c4c7e86bc10d16" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "game_team_user" ADD CONSTRAINT "FK_dea2174a735e6547fa42537015c" FOREIGN KEY ("game_id") REFERENCES "game"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "game_team_user" ADD CONSTRAINT "FK_359197ec4d167804a84586689b2" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "game_team_user" DROP CONSTRAINT "FK_359197ec4d167804a84586689b2"`);
        await queryRunner.query(`ALTER TABLE "game_team_user" DROP CONSTRAINT "FK_dea2174a735e6547fa42537015c"`);
        await queryRunner.query(`ALTER TABLE "game" DROP CONSTRAINT "FK_678fcc30dbaf1c4c7e86bc10d16"`);
        await queryRunner.query(`ALTER TABLE "game_version" DROP CONSTRAINT "FK_5a4e407c2898e29b00136632b33"`);
        await queryRunner.query(`ALTER TABLE "game_feedback_response" DROP CONSTRAINT "FK_b27f98ad277a0e2418f4dddbe5a"`);
        await queryRunner.query(`ALTER TABLE "game_feedback_response" DROP CONSTRAINT "FK_7e65414778ce14d3b69c7392db4"`);
        await queryRunner.query(`CREATE TYPE "public"."proposal_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" "public"."proposal_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`CREATE TYPE "public"."course_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "course" ADD "type" "public"."course_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`CREATE TYPE "public"."chapter_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "type" "public"."chapter_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`CREATE TYPE "public"."lecture_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD "type" "public"."lecture_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`CREATE TYPE "public"."post_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "post" ADD "type" "public"."post_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`DROP INDEX "public"."IDX_359197ec4d167804a84586689b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_dea2174a735e6547fa42537015"`);
        await queryRunner.query(`DROP TABLE "game_team_user"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_8ea221926a26ee68a25c904b65"`);
        await queryRunner.query(`DROP TABLE "game"`);
        await queryRunner.query(`DROP TYPE "public"."game_visibility_enum"`);
        await queryRunner.query(`DROP TABLE "game_version"`);
        await queryRunner.query(`DROP TABLE "game_feedback_response"`);
    }

}
