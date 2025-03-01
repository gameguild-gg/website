import { MigrationInterface, QueryRunner } from "typeorm";

export class FixTypesCMS1710713266891 implements MigrationInterface {
    name = 'FixTypesCMS1710713266891'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TYPE "public"."lecture_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`CREATE TYPE "public"."lecture_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`CREATE TABLE "lecture" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "slug" character varying(255) DEFAULT '', "title" character varying(1024) DEFAULT '', "summary" character varying(1024) DEFAULT '', "body" jsonb DEFAULT '{}', "visibility" "public"."lecture_visibility_enum" NOT NULL DEFAULT 'DRAFT', "thumbnail" character varying DEFAULT '', "type" "public"."lecture_type_enum" NOT NULL DEFAULT 'NONE', "order" double precision NOT NULL DEFAULT '0', "course_id" uuid, "chapter_id" uuid, CONSTRAINT "PK_2abef7c1e52b7b58a9f905c9643" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_f5cadec277927351eb00098dd7" ON "lecture" ("visibility") `);
        await queryRunner.query(`CREATE INDEX "IDX_c6c3264ce6966d5c1d078e147b" ON "lecture" ("order") `);
        await queryRunner.query(`CREATE TABLE "user_posts_post" ("user_id" uuid NOT NULL, "post_id" uuid NOT NULL, CONSTRAINT "PK_c46184b54cfec2342e1c575b00b" PRIMARY KEY ("user_id", "post_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_42791e638b2f587a61232e6408" ON "user_posts_post" ("user_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_557997292328c6f127dbee211b" ON "user_posts_post" ("post_id") `);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "lexical"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "lexical"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "body" jsonb DEFAULT '{}'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`CREATE TYPE "public"."proposal_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" "public"."proposal_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`ALTER TABLE "post" ADD "slug" character varying(255) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "post" ADD "title" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "post" ADD "summary" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "post" ADD "body" jsonb DEFAULT '{}'`);
        await queryRunner.query(`CREATE TYPE "public"."post_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`ALTER TABLE "post" ADD "visibility" "public"."post_visibility_enum" NOT NULL DEFAULT 'DRAFT'`);
        await queryRunner.query(`ALTER TABLE "post" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`CREATE TYPE "public"."post_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "post" ADD "type" "public"."post_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`CREATE TYPE "public"."post_post_type_enum" AS ENUM('BLOG')`);
        await queryRunner.query(`ALTER TABLE "post" ADD "post_type" "public"."post_post_type_enum" NOT NULL DEFAULT 'BLOG'`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "slug" character varying(255) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "title" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "summary" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "body" jsonb DEFAULT '{}'`);
        await queryRunner.query(`CREATE TYPE "public"."chapter_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "visibility" "public"."chapter_visibility_enum" NOT NULL DEFAULT 'DRAFT'`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`CREATE TYPE "public"."chapter_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "type" "public"."chapter_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "order" double precision NOT NULL DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "course_id" uuid`);
        await queryRunner.query(`ALTER TABLE "course" ADD "body" jsonb DEFAULT '{}'`);
        await queryRunner.query(`ALTER TABLE "course" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`CREATE TYPE "public"."course_type_enum" AS ENUM('LECTURE', 'POST', 'RESOURCE', 'LINK', 'EMBED', 'IPFS', 'VIDEO', 'TEXT', 'QUIZ', 'ASSIGNMENT', 'EXAM', 'DISCUSSION', 'SURVEY', 'POLL', 'INTERACTIVE', 'AUDIO', 'PDF', 'SLIDES', 'IMAGE', 'CODE', 'PRESENTATION', 'DOCUMENT', 'SPREADSHEET', 'FORM', 'DRAWING', 'MAP', 'EBOOK', 'OTHER', 'NONE')`);
        await queryRunner.query(`ALTER TABLE "course" ADD "type" "public"."course_type_enum" NOT NULL DEFAULT 'NONE'`);
        await queryRunner.query(`ALTER TABLE "course" ADD "author_id" uuid`);
        await queryRunner.query(`CREATE INDEX "IDX_a80ca3bf4ca3711c488cb82cf7" ON "post" ("visibility") `);
        await queryRunner.query(`CREATE INDEX "IDX_859d333ca13d040bf2f941afcd" ON "chapter" ("visibility") `);
        await queryRunner.query(`CREATE INDEX "IDX_2c985baf03bef490c97e168cdc" ON "chapter" ("order") `);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_8617e94525034849ee1b9b2e2f1" FOREIGN KEY ("course_id") REFERENCES "course"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab" FOREIGN KEY ("chapter_id") REFERENCES "course"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD CONSTRAINT "FK_be4eebd798cc26bd6bded42f8c0" FOREIGN KEY ("course_id") REFERENCES "course"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "course" ADD CONSTRAINT "FK_907888662706db7def5705ef5d3" FOREIGN KEY ("author_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "user_posts_post" ADD CONSTRAINT "FK_42791e638b2f587a61232e64081" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "user_posts_post" ADD CONSTRAINT "FK_557997292328c6f127dbee211b0" FOREIGN KEY ("post_id") REFERENCES "post"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user_posts_post" DROP CONSTRAINT "FK_557997292328c6f127dbee211b0"`);
        await queryRunner.query(`ALTER TABLE "user_posts_post" DROP CONSTRAINT "FK_42791e638b2f587a61232e64081"`);
        await queryRunner.query(`ALTER TABLE "course" DROP CONSTRAINT "FK_907888662706db7def5705ef5d3"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP CONSTRAINT "FK_be4eebd798cc26bd6bded42f8c0"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_f3dd4ae5b548ce2d8dfc2f887ab"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_8617e94525034849ee1b9b2e2f1"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_2c985baf03bef490c97e168cdc"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_859d333ca13d040bf2f941afcd"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_a80ca3bf4ca3711c488cb82cf7"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "author_id"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."course_type_enum"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "body"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "course_id"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "order"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."chapter_type_enum"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "visibility"`);
        await queryRunner.query(`DROP TYPE "public"."chapter_visibility_enum"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "body"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "summary"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "post_type"`);
        await queryRunner.query(`DROP TYPE "public"."post_post_type_enum"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."post_type_enum"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "visibility"`);
        await queryRunner.query(`DROP TYPE "public"."post_visibility_enum"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "body"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "summary"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."proposal_type_enum"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "body"`);
        await queryRunner.query(`ALTER TABLE "course" ADD "lexical" jsonb DEFAULT '{}'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "lexical" jsonb DEFAULT '{}'`);
        await queryRunner.query(`DROP INDEX "public"."IDX_557997292328c6f127dbee211b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_42791e638b2f587a61232e6408"`);
        await queryRunner.query(`DROP TABLE "user_posts_post"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_c6c3264ce6966d5c1d078e147b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_f5cadec277927351eb00098dd7"`);
        await queryRunner.query(`DROP TABLE "lecture"`);
        await queryRunner.query(`DROP TYPE "public"."lecture_type_enum"`);
        await queryRunner.query(`DROP TYPE "public"."lecture_visibility_enum"`);
    }

}
