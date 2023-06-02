import { MigrationInterface, QueryRunner } from "typeorm";

export class ExtractProfileFromUser1685738837978 implements MigrationInterface {
    name = 'ExtractProfileFromUser1685738837978'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_dfaef34317c44a58477057b1946"`);
        await queryRunner.query(`ALTER TABLE "event" DROP CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
        await queryRunner.query(`CREATE TABLE "user-profile" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "full_name" character varying NOT NULL, "email" character varying NOT NULL, "cpf" character varying NOT NULL, CONSTRAINT "PK_648947e3e523dcf4c78ec8b7bcf" PRIMARY KEY ("id"))`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "description"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "category"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "proposal_id"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "firstname"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "lastname"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "email"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "cpf"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "proposal_id" uuid`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "title" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "description" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "category" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ADD "profile_id" uuid`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_f44d0cd18cfd80b0fed7806c3b7" UNIQUE ("profile_id")`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "role"`);
        await queryRunner.query(`DROP TYPE "public"."user_role_enum"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "role" character varying NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
        await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
        await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_dfaef34317c44a58477057b1946" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "event" ADD CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105" FOREIGN KEY ("creator_id") REFERENCES "user-profile"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "FK_f44d0cd18cfd80b0fed7806c3b7" FOREIGN KEY ("profile_id") REFERENCES "user-profile"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "FK_f44d0cd18cfd80b0fed7806c3b7"`);
        await queryRunner.query(`ALTER TABLE "event" DROP CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_dfaef34317c44a58477057b1946"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "role"`);
        await queryRunner.query(`CREATE TYPE "public"."user_role_enum" AS ENUM('Admin', 'Content creator', 'User')`);
        await queryRunner.query(`ALTER TABLE "user" ADD "role" "public"."user_role_enum" NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_f44d0cd18cfd80b0fed7806c3b7"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "profile_id"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "category"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "description"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "proposal_id"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "cpf" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ADD "email" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ADD "lastname" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ADD "firstname" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "proposal_id" uuid`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "category" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "description" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "title" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`DROP TABLE "user-profile"`);
        await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
        await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
        await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
        await queryRunner.query(`ALTER TABLE "event" ADD CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105" FOREIGN KEY ("creator_id") REFERENCES "user-data"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_dfaef34317c44a58477057b1946" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
