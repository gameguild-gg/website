import { MigrationInterface, QueryRunner } from "typeorm";

export class MonorepoReactorAuth1698693554467 implements MigrationInterface {
  name = 'MonorepoReactorAuth1698693554467'

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "FK_f44d0cd18cfd80b0fed7806c3b7"`);
    await queryRunner.query(`ALTER TABLE "vote" DROP CONSTRAINT "FK_db85a3f8526cbaa2865faf8637f"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
    await queryRunner.query(`CREATE TABLE "user_profile" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), CONSTRAINT "PK_f44d0cd18cfd80b0fed7806c3b7" PRIMARY KEY ("id"))`);
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "description"`);
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "category"`);
    await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "role"`);
    await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "REL_f44d0cd18cfd80b0fed7806c3b"`);
    await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "profile_id"`);
    await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "email_validated"`);
    await queryRunner.query(`ALTER TABLE "vote" DROP COLUMN "proposal_id"`);
    await queryRunner.query(`ALTER TABLE "user" ADD "email_verified" boolean NOT NULL DEFAULT false`);
    await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_78a916df40e02a9deb1c4b75edb" UNIQUE ("username")`);
    await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_e12875dfb3b1d92d7d7c5377e22" UNIQUE ("email")`);
    await queryRunner.query(`CREATE INDEX "IDX_78a916df40e02a9deb1c4b75ed" ON "user" ("username") `);
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`DROP INDEX "public"."IDX_78a916df40e02a9deb1c4b75ed"`);
    await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_e12875dfb3b1d92d7d7c5377e22"`);
    await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_78a916df40e02a9deb1c4b75edb"`);
    await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "email_verified"`);
    await queryRunner.query(`ALTER TABLE "vote" ADD "proposal_id" uuid`);
    await queryRunner.query(`ALTER TABLE "user" ADD "email_validated" boolean NOT NULL DEFAULT false`);
    await queryRunner.query(`ALTER TABLE "user" ADD "profile_id" uuid`);
    await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "REL_f44d0cd18cfd80b0fed7806c3b" UNIQUE ("profile_id")`);
    await queryRunner.query(`ALTER TABLE "user" ADD "role" character varying NOT NULL DEFAULT 'User'`);
    await queryRunner.query(`ALTER TABLE "proposal" ADD "category" character varying NOT NULL`);
    await queryRunner.query(`ALTER TABLE "proposal" ADD "type" character varying NOT NULL`);
    await queryRunner.query(`ALTER TABLE "proposal" ADD "description" text NOT NULL DEFAULT 'untitled'`);
    await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" text NOT NULL DEFAULT 'untitled'`);
    await queryRunner.query(`ALTER TABLE "proposal" ADD "title" text NOT NULL DEFAULT 'untitled'`);
    await queryRunner.query(`DROP TABLE "user_profile"`);
    await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
    await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
    await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
    await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
    await queryRunner.query(`ALTER TABLE "vote" ADD CONSTRAINT "FK_db85a3f8526cbaa2865faf8637f" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "FK_f44d0cd18cfd80b0fed7806c3b7" FOREIGN KEY ("profile_id") REFERENCES "user-profile"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
  }

}
