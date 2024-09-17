import { MigrationInterface, QueryRunner } from "typeorm";

export class AddedTicket1726611299621 implements MigrationInterface {
    name = 'AddedTicket1726611299621'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "ticket" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "owner_id" uuid NOT NULL, "project_id" uuid, CONSTRAINT "PK_d9a0835407701eb86f874474b7c" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "ticket_editors_user" ("ticket_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_cbe7f143227c03a658062234a49" PRIMARY KEY ("ticket_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_1d5c90934caa7c88aef8de3798" ON "ticket_editors_user" ("ticket_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_68a614c8fd4410c06e03e0fa84" ON "ticket_editors_user" ("user_id") `);
        await queryRunner.query(`ALTER TABLE "ticket" ADD CONSTRAINT "FK_eb16ef58c3c71cf333b9cc106f0" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "ticket" ADD CONSTRAINT "FK_17cc52872004472c7a8125fd789" FOREIGN KEY ("project_id") REFERENCES "project"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" ADD CONSTRAINT "FK_1d5c90934caa7c88aef8de3798d" FOREIGN KEY ("ticket_id") REFERENCES "ticket"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" ADD CONSTRAINT "FK_68a614c8fd4410c06e03e0fa844" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" DROP CONSTRAINT "FK_68a614c8fd4410c06e03e0fa844"`);
        await queryRunner.query(`ALTER TABLE "ticket_editors_user" DROP CONSTRAINT "FK_1d5c90934caa7c88aef8de3798d"`);
        await queryRunner.query(`ALTER TABLE "ticket" DROP CONSTRAINT "FK_17cc52872004472c7a8125fd789"`);
        await queryRunner.query(`ALTER TABLE "ticket" DROP CONSTRAINT "FK_eb16ef58c3c71cf333b9cc106f0"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_68a614c8fd4410c06e03e0fa84"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_1d5c90934caa7c88aef8de3798"`);
        await queryRunner.query(`DROP TABLE "ticket_editors_user"`);
        await queryRunner.query(`DROP TABLE "ticket"`);
    }

}
