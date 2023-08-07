import { MigrationInterface, QueryRunner } from "typeorm";

export class InitialDatatypes1685629877979 implements MigrationInterface {
    name = 'InitialDatatypes1685629877979'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_dfaef34317c44a58477057b1946"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_894de7603d8172deb20277c6f5"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_788a2da76636d59b8803d21968"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4bbf141d0e9ef16b100fedf1a4"`);
        await queryRunner.query(`CREATE TABLE "chapter" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), CONSTRAINT "PK_275bd1c62bed7dff839680614ca" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "post" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), CONSTRAINT "PK_be5fda3aac270b134ff9c21cdee" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "course" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), CONSTRAINT "PK_bf95180dd756fd204fb01ce4916" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TYPE "public"."user_role_enum" AS ENUM('Admin', 'Content creator', 'User')`);
        await queryRunner.query(`CREATE TABLE "user" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "username" character varying NOT NULL, "firstname" character varying NOT NULL, "lastname" character varying NOT NULL, "email" character varying NOT NULL, "cpf" character varying NOT NULL, "role" "public"."user_role_enum" NOT NULL, "active" boolean NOT NULL, CONSTRAINT "PK_cace4a159ff9f2512dd42373760" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "event" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "title" character varying NOT NULL, "day" TIMESTAMP NOT NULL, "time" TIMESTAMP NOT NULL, "type" character varying NOT NULL, "creator_id" uuid, CONSTRAINT "PK_30c2f3bbaf6d34a55f8ae6e4614" PRIMARY KEY ("id"))`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "slug"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "description"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."proposal_type_enum"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "category"`);
        await queryRunner.query(`DROP TYPE "public"."proposal_category_enum"`);
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
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_dfaef34317c44a58477057b1946" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "event" ADD CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105" FOREIGN KEY ("creator_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "event" DROP CONSTRAINT "FK_25cfbefb1f85a771e03c6cbf105"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_dfaef34317c44a58477057b1946"`);
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
        await queryRunner.query(`CREATE TYPE "public"."proposal_category_enum" AS ENUM('coding', 'design', 'art', 'music', 'writing')`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "category" "public"."proposal_category_enum" NOT NULL`);
        await queryRunner.query(`CREATE TYPE "public"."proposal_type_enum" AS ENUM('rfc', 'codingdojo', 'workshop', 'jam', 'lecture', 'bootcamp', 'contest')`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "type" "public"."proposal_type_enum" NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "description" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "slug" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "title" text NOT NULL DEFAULT 'untitled'`);
        await queryRunner.query(`DROP TABLE "event"`);
        await queryRunner.query(`DROP TABLE "user"`);
        await queryRunner.query(`DROP TYPE "public"."user_role_enum"`);
        await queryRunner.query(`DROP TABLE "course"`);
        await queryRunner.query(`DROP TABLE "post"`);
        await queryRunner.query(`DROP TABLE "chapter"`);
        await queryRunner.query(`CREATE INDEX "IDX_4bbf141d0e9ef16b100fedf1a4" ON "proposal" ("category") `);
        await queryRunner.query(`CREATE INDEX "IDX_788a2da76636d59b8803d21968" ON "proposal" ("type") `);
        await queryRunner.query(`CREATE INDEX "IDX_894de7603d8172deb20277c6f5" ON "proposal" ("slug") `);
        await queryRunner.query(`CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_dfaef34317c44a58477057b1946" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
