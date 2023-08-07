import { MigrationInterface, QueryRunner } from "typeorm";

export class Boilerplate1683865241905 implements MigrationInterface {
    name = 'Boilerplate1683865241905'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "proposal" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "proposal_id" uuid, CONSTRAINT "PK_ca872ecfe4fef5720d2d39e4275" PRIMARY KEY ("id"))`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "proposal_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "proposal_id" uuid`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "title" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "description" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`CREATE TYPE "public"."proposal_type_enum" AS ENUM('rfc', 'codingdojo', 'workshop', 'jam', 'lecture', 'bootcamp', 'contest')`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" "public"."proposal_type_enum" NOT NULL`);
        await queryRunner.query(`CREATE TYPE "public"."proposal_category_enum" AS ENUM('coding', 'design', 'art', 'music', 'writing')`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "category" "public"."proposal_category_enum" NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
        await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
        await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_dfaef34317c44a58477057b1946" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_dfaef34317c44a58477057b1946"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "category"`);
        await queryRunner.query(`DROP TYPE "public"."proposal_category_enum"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."proposal_type_enum"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "description"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "proposal_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "proposal_id" uuid`);
        await queryRunner.query(`DROP TABLE "proposal"`);
    }
}
