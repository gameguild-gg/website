import { MigrationInterface, QueryRunner } from "typeorm";

export class AddingTicketcontroller1726962407597 implements MigrationInterface {
    name = 'AddingTicketcontroller1726962407597'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" DROP CONSTRAINT "FK_1d5c90934caa7c88aef8de3798d"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_1d5c90934caa7c88aef8de3798"`);
        await queryRunner.query(`ALTER TABLE "ticket" ADD "ticket_number" SERIAL NOT NULL`);
        await queryRunner.query(`ALTER TABLE "ticket" DROP CONSTRAINT "PK_d9a0835407701eb86f874474b7c"`);
        await queryRunner.query(`ALTER TABLE "ticket" ADD CONSTRAINT "PK_d4bc3a258983a70c9e508d45d00" PRIMARY KEY ("id", "ticket_number")`);
        await queryRunner.query(`ALTER TABLE "project" ADD "description" character varying(255)`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" ADD "ticket_ticket_number" integer NOT NULL`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" DROP CONSTRAINT "PK_cbe7f143227c03a658062234a49"`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" ADD CONSTRAINT "PK_e3c1f4331a2e0d49242fd798e3d" PRIMARY KEY ("ticket_id", "user_id", "ticket_ticket_number")`);
        await queryRunner.query(`CREATE INDEX "IDX_4d2bcbc0f24e9c7fbfff739057" ON "ticket_editors_user" ("ticket_id", "ticket_ticket_number") `);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" ADD CONSTRAINT "FK_4d2bcbc0f24e9c7fbfff7390577" FOREIGN KEY ("ticket_id", "ticket_ticket_number") REFERENCES "ticket"("id","ticket_number") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" DROP CONSTRAINT "FK_4d2bcbc0f24e9c7fbfff7390577"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4d2bcbc0f24e9c7fbfff739057"`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" DROP CONSTRAINT "PK_e3c1f4331a2e0d49242fd798e3d"`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" ADD CONSTRAINT "PK_cbe7f143227c03a658062234a49" PRIMARY KEY ("ticket_id", "user_id")`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" DROP COLUMN "ticket_ticket_number"`);
        await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "description"`);
        await queryRunner.query(`ALTER TABLE "ticket" DROP CONSTRAINT "PK_d4bc3a258983a70c9e508d45d00"`);
        await queryRunner.query(`ALTER TABLE "ticket" ADD CONSTRAINT "PK_d9a0835407701eb86f874474b7c" PRIMARY KEY ("id")`);
        await queryRunner.query(`ALTER TABLE "ticket" DROP COLUMN "ticket_number"`);
        await queryRunner.query(`CREATE INDEX "IDX_1d5c90934caa7c88aef8de3798" ON "ticket_editors_user" ("ticket_id") `);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" ADD CONSTRAINT "FK_1d5c90934caa7c88aef8de3798d" FOREIGN KEY ("ticket_id") REFERENCES "ticket"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

}
