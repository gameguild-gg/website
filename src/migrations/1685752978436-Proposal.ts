import { MigrationInterface, QueryRunner } from "typeorm";

export class Proposal1685752978436 implements MigrationInterface {
    name = 'Proposal1685752978436'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "vote" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "proposal_id" uuid, CONSTRAINT "PK_2d5932d46afe39c8176f9d4be72" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "proposal" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "title" text NOT NULL DEFAULT 'untitled', "slug" text NOT NULL DEFAULT 'untitled', "description" text NOT NULL DEFAULT 'untitled', "type" character varying NOT NULL, "category" character varying NOT NULL, CONSTRAINT "PK_ca872ecfe4fef5720d2d39e4275" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
        await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
        await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
        await queryRunner.query(`ALTER TABLE "vote" ADD CONSTRAINT "FK_db85a3f8526cbaa2865faf8637f" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "vote" DROP CONSTRAINT "FK_db85a3f8526cbaa2865faf8637f"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
        await queryRunner.query(`DROP TABLE "proposal"`);
        await queryRunner.query(`DROP TABLE "vote"`);
    }
}
