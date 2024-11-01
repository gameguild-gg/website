import { MigrationInterface, QueryRunner } from "typeorm";

export class AddJobTables1728923936936 implements MigrationInterface {
    name = 'AddJobTables1728923936936'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-tag" DROP CONSTRAINT "FK_281854bc897857ec3fe3412bef8"`);
        await queryRunner.query(`ALTER TABLE "job-tag" RENAME COLUMN "job_id" TO "deleted_at"`);
        await queryRunner.query(`CREATE TABLE "job-application" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "deleted_at" TIMESTAMP, "progress" integer NOT NULL DEFAULT '0', "applicant_id" uuid NOT NULL, "job_id" uuid, CONSTRAINT "PK_896c8c02d7da2c0228d586e54b4" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "job-post_job_tags_job-tag" ("job-post_id" uuid NOT NULL, "job-tag_id" uuid NOT NULL, CONSTRAINT "PK_7a49c7ad19ebb3e814e2918012f" PRIMARY KEY ("job-post_id", "job-tag_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_251949f6aae5f190956d20ed63" ON "job-post_job_tags_job-tag" ("job-post_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_044f1ccbefb86ed608a0a17c3b" ON "job-post_job_tags_job-tag" ("job-tag_id") `);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "deleted_at" TIMESTAMP`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "location" character varying(256) NOT NULL DEFAULT 'Remote'`);
        await queryRunner.query(`CREATE TYPE "public"."job-post_job_type_enum" AS ENUM('CONTINUOUS', 'TASK')`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "job_type" "public"."job-post_job_type_enum" NOT NULL DEFAULT 'TASK'`);
        await queryRunner.query(`ALTER TABLE "ticket" ADD "deleted_at" TIMESTAMP`);
        await queryRunner.query(`ALTER TABLE "post" DROP CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c"`);
        await queryRunner.query(`ALTER TABLE "post" ALTER COLUMN "owner_id" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_e61feac3d2667f7f077955c2891"`);
        await queryRunner.query(`ALTER TABLE "lecture" ALTER COLUMN "owner_id" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f"`);
        await queryRunner.query(`ALTER TABLE "chapter" ALTER COLUMN "owner_id" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "course" DROP CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65"`);
        await queryRunner.query(`ALTER TABLE "course" ALTER COLUMN "owner_id" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29"`);
        await queryRunner.query(`ALTER TABLE "proposal" ALTER COLUMN "owner_id" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-tag" DROP COLUMN "deleted_at"`);
        await queryRunner.query(`ALTER TABLE "job-tag" ADD "deleted_at" TIMESTAMP`);
        await queryRunner.query(`ALTER TABLE "proposal" ALTER COLUMN "owner_id" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "project" DROP CONSTRAINT "FK_d40afe32d1d771bea7a5f468185"`);
        await queryRunner.query(`ALTER TABLE "project" ALTER COLUMN "owner_id" DROP NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_a84bb70f034d1ba5605cd3fbc5" ON "job-post" ("location") `);
        await queryRunner.query(`CREATE INDEX "IDX_4914621fb42025de12e8ec2225" ON "job-post" ("job_type") `);
        await queryRunner.query(`ALTER TABLE "post" ADD CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_e61feac3d2667f7f077955c2891" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "course" ADD CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "job-application" ADD CONSTRAINT "FK_ca676357efe051c6135232d3ffc" FOREIGN KEY ("applicant_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "job-application" ADD CONSTRAINT "FK_d88c856eb48cbc63bde511c6cad" FOREIGN KEY ("job_id") REFERENCES "job-post"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "project" ADD CONSTRAINT "FK_d40afe32d1d771bea7a5f468185" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "job-post_job_tags_job-tag" ADD CONSTRAINT "FK_251949f6aae5f190956d20ed635" FOREIGN KEY ("job-post_id") REFERENCES "job-post"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "job-post_job_tags_job-tag" ADD CONSTRAINT "FK_044f1ccbefb86ed608a0a17c3bd" FOREIGN KEY ("job-tag_id") REFERENCES "job-tag"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-post_job_tags_job-tag" DROP CONSTRAINT "FK_044f1ccbefb86ed608a0a17c3bd"`);
        await queryRunner.query(`ALTER TABLE "job-post_job_tags_job-tag" DROP CONSTRAINT "FK_251949f6aae5f190956d20ed635"`);
        await queryRunner.query(`ALTER TABLE "project" DROP CONSTRAINT "FK_d40afe32d1d771bea7a5f468185"`);
        await queryRunner.query(`ALTER TABLE "job-application" DROP CONSTRAINT "FK_d88c856eb48cbc63bde511c6cad"`);
        await queryRunner.query(`ALTER TABLE "job-application" DROP CONSTRAINT "FK_ca676357efe051c6135232d3ffc"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29"`);
        await queryRunner.query(`ALTER TABLE "course" DROP CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_e61feac3d2667f7f077955c2891"`);
        await queryRunner.query(`ALTER TABLE "post" DROP CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4914621fb42025de12e8ec2225"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_a84bb70f034d1ba5605cd3fbc5"`);
        await queryRunner.query(`ALTER TABLE "project" ALTER COLUMN "owner_id" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "project" ADD CONSTRAINT "FK_d40afe32d1d771bea7a5f468185" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "proposal" ALTER COLUMN "owner_id" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-tag" DROP COLUMN "deleted_at"`);
        await queryRunner.query(`ALTER TABLE "job-tag" ADD "deleted_at" uuid`);
        await queryRunner.query(`ALTER TABLE "proposal" ALTER COLUMN "owner_id" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "course" ALTER COLUMN "owner_id" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "course" ADD CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "chapter" ALTER COLUMN "owner_id" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "lecture" ALTER COLUMN "owner_id" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_e61feac3d2667f7f077955c2891" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "post" ALTER COLUMN "owner_id" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "post" ADD CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "ticket" DROP COLUMN "deleted_at"`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "job_type"`);
        await queryRunner.query(`DROP TYPE "public"."job-post_job_type_enum"`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "location"`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "deleted_at"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_044f1ccbefb86ed608a0a17c3b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_251949f6aae5f190956d20ed63"`);
        await queryRunner.query(`DROP TABLE "job-post_job_tags_job-tag"`);
        await queryRunner.query(`DROP TABLE "job-application"`);
        await queryRunner.query(`ALTER TABLE "job-tag" RENAME COLUMN "deleted_at" TO "job_id"`);
        await queryRunner.query(`ALTER TABLE "job-tag" ADD CONSTRAINT "FK_281854bc897857ec3fe3412bef8" FOREIGN KEY ("job_id") REFERENCES "job-post"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
