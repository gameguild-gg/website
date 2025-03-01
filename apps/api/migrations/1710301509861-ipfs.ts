import { MigrationInterface, QueryRunner } from "typeorm";

export class Ipfs1710301509861 implements MigrationInterface {
    name = 'Ipfs1710301509861'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "tag" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "tag" character varying(50) NOT NULL DEFAULT '', CONSTRAINT "PK_8e4052373c579afc1471f526760" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_9dbf61b2d00d2c77d3b5ced37c" ON "tag" ("tag") `);
        await queryRunner.query(`CREATE TABLE "ipfs" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "cid" character varying NOT NULL, "mime_type" character varying NOT NULL, "file_name" character varying NOT NULL, CONSTRAINT "PK_af553336f95ba7f14152e2048ee" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_fe9f97ee018e50ccb5d5255f56" ON "ipfs" ("cid") `);
        await queryRunner.query(`CREATE INDEX "IDX_f148f9547be0e098ecc0ec27db" ON "ipfs" ("mime_type") `);
        await queryRunner.query(`CREATE INDEX "IDX_23428c03d5433c30783c9efe4a" ON "ipfs" ("file_name") `);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "lexical" jsonb DEFAULT '{}'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "title" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" character varying(255) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "summary" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`CREATE TYPE "public"."proposal_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "visibility" "public"."proposal_visibility_enum" NOT NULL DEFAULT 'DRAFT'`);
        await queryRunner.query(`ALTER TABLE "course" ADD "lexical" jsonb DEFAULT '{}'`);
        await queryRunner.query(`ALTER TABLE "course" ADD "title" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "course" ADD "slug" character varying(255) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "course" ADD "summary" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`CREATE TYPE "public"."course_visibility_enum" AS ENUM('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH')`);
        await queryRunner.query(`ALTER TABLE "course" ADD "visibility" "public"."course_visibility_enum" NOT NULL DEFAULT 'DRAFT'`);
        await queryRunner.query(`ALTER TABLE "course" ADD "price" double precision DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "course" ADD "subscription_access" boolean NOT NULL DEFAULT true`);
        await queryRunner.query(`CREATE INDEX "IDX_f0f2c5e15690f642f3afcde829" ON "proposal" ("visibility") `);
        await queryRunner.query(`CREATE INDEX "IDX_653293097239013a0e49f481f9" ON "course" ("visibility") `);
        await queryRunner.query(`CREATE INDEX "IDX_4f1c8311d8e665e0d0c0046326" ON "course" ("price") `);
        await queryRunner.query(`CREATE INDEX "IDX_399d4e499683d688c07a41da26" ON "course" ("subscription_access") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_399d4e499683d688c07a41da26"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4f1c8311d8e665e0d0c0046326"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_653293097239013a0e49f481f9"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_f0f2c5e15690f642f3afcde829"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "subscription_access"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "price"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "visibility"`);
        await queryRunner.query(`DROP TYPE "public"."course_visibility_enum"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "summary"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "lexical"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "visibility"`);
        await queryRunner.query(`DROP TYPE "public"."proposal_visibility_enum"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "summary"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "lexical"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_23428c03d5433c30783c9efe4a"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_f148f9547be0e098ecc0ec27db"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_fe9f97ee018e50ccb5d5255f56"`);
        await queryRunner.query(`DROP TABLE "ipfs"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_9dbf61b2d00d2c77d3b5ced37c"`);
        await queryRunner.query(`DROP TABLE "tag"`);
    }

}
