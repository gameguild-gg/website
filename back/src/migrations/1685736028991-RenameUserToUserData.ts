import { MigrationInterface, QueryRunner } from "typeorm";

export class RenameUserToUserData1685736028991 implements MigrationInterface {
    name = 'RenameUserToUserData1685736028991'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "event" DROP CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_dfaef34317c44a58477057b1946"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
        await queryRunner.query(`CREATE TABLE "user-data" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "username" character varying NOT NULL, "firstname" character varying NOT NULL, "lastname" character varying NOT NULL, "email" character varying NOT NULL, "cpf" character varying NOT NULL, "role" character varying NOT NULL, "active" boolean NOT NULL, CONSTRAINT "PK_9d1aa63033ca5bae9ea064098ad" PRIMARY KEY ("id"))`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "description"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "category"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "proposal_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "proposal_id" uuid`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "title" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "description" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "category" character varying NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
        await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
        await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
        await queryRunner.query(`ALTER TABLE "event" ADD CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105" FOREIGN KEY ("creator_id") REFERENCES "user-data"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_dfaef34317c44a58477057b1946" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_dfaef34317c44a58477057b1946"`);
        await queryRunner.query(`ALTER TABLE "event" DROP CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "category"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "description"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "proposal_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "proposal_id" uuid`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "category" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "description" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "title" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`DROP TABLE "user-data"`);
        await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
        await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
        await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_dfaef34317c44a58477057b1946" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "event" ADD CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105" FOREIGN KEY ("creator_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
