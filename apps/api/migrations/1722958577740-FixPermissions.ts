import { MigrationInterface, QueryRunner } from "typeorm";

export class FixPermissions1722958577740 implements MigrationInterface {
    name = 'FixPermissions1722958577740'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "game" RENAME COLUMN "roles" TO "owner_id"`);
        await queryRunner.query(`CREATE TABLE "post_editors_user" ("post_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_4399b494a9644933edf0e0d7fcb" PRIMARY KEY ("post_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_9fc34bb51d7a8a50a875dbeff2" ON "post_editors_user" ("post_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_4abfcfa0b8814c3522eb488636" ON "post_editors_user" ("user_id") `);
        await queryRunner.query(`CREATE TABLE "lecture_editors_user" ("lecture_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_6cd319be02ecdbfb6b78a3c646c" PRIMARY KEY ("lecture_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_a11c650256fe110cdaa8be2579" ON "lecture_editors_user" ("lecture_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_88ce9c7b7ef0b39989593ab150" ON "lecture_editors_user" ("user_id") `);
        await queryRunner.query(`CREATE TABLE "chapter_editors_user" ("chapter_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_6b677152cc31755de3d795224f6" PRIMARY KEY ("chapter_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_6bc76308984eee4bb9c49d9b2a" ON "chapter_editors_user" ("chapter_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_22ec2de78ee0f0e2e7ee768a4c" ON "chapter_editors_user" ("user_id") `);
        await queryRunner.query(`CREATE TABLE "course_editors_user" ("course_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_a863e83f726a4e6f0704fcd302d" PRIMARY KEY ("course_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_f0f27df7244631f69b3aa42a0d" ON "course_editors_user" ("course_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_25fff6cb34e063caab1eb4c7c1" ON "course_editors_user" ("user_id") `);
        await queryRunner.query(`CREATE TABLE "proposal_editors_user" ("proposal_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_3fead86d04749b09cebb8fd953d" PRIMARY KEY ("proposal_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_d5efb3b9d5fb4cd25e2f1eb849" ON "proposal_editors_user" ("proposal_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_2469607eeb33fa61f32bda5326" ON "proposal_editors_user" ("user_id") `);
        await queryRunner.query(`ALTER TABLE "post" ADD "owner_id" uuid NOT NULL`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD "owner_id" uuid NOT NULL`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "owner_id" uuid NOT NULL`);
        await queryRunner.query(`ALTER TABLE "course" ADD "owner_id" uuid NOT NULL`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "owner_id" uuid NOT NULL`);
        await queryRunner.query(`ALTER TABLE "game" DROP COLUMN "owner_id"`);
        await queryRunner.query(`ALTER TABLE "game" ADD "owner_id" uuid NOT NULL`);
        await queryRunner.query(`ALTER TABLE "post" ADD CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD CONSTRAINT "FK_e61feac3d2667f7f077955c2891" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "course" ADD CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "game" ADD CONSTRAINT "FK_678fcc30dbaf1c4c7e86bc10d16" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "post_editors_user" ADD CONSTRAINT "FK_9fc34bb51d7a8a50a875dbeff29" FOREIGN KEY ("post_id") REFERENCES "post"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "post_editors_user" ADD CONSTRAINT "FK_4abfcfa0b8814c3522eb488636a" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "lecture_editors_user" ADD CONSTRAINT "FK_a11c650256fe110cdaa8be2579c" FOREIGN KEY ("lecture_id") REFERENCES "lecture"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "lecture_editors_user" ADD CONSTRAINT "FK_88ce9c7b7ef0b39989593ab1505" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" ADD CONSTRAINT "FK_6bc76308984eee4bb9c49d9b2a7" FOREIGN KEY ("chapter_id") REFERENCES "chapter"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" ADD CONSTRAINT "FK_22ec2de78ee0f0e2e7ee768a4c7" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "course_editors_user" ADD CONSTRAINT "FK_f0f27df7244631f69b3aa42a0dd" FOREIGN KEY ("course_id") REFERENCES "course"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "course_editors_user" ADD CONSTRAINT "FK_25fff6cb34e063caab1eb4c7c17" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "proposal_editors_user" ADD CONSTRAINT "FK_d5efb3b9d5fb4cd25e2f1eb8492" FOREIGN KEY ("proposal_id") REFERENCES "proposal"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "proposal_editors_user" ADD CONSTRAINT "FK_2469607eeb33fa61f32bda5326b" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "proposal_editors_user" DROP CONSTRAINT "FK_2469607eeb33fa61f32bda5326b"`);
        await queryRunner.query(`ALTER TABLE "proposal_editors_user" DROP CONSTRAINT "FK_d5efb3b9d5fb4cd25e2f1eb8492"`);
        await queryRunner.query(`ALTER TABLE "course_editors_user" DROP CONSTRAINT "FK_25fff6cb34e063caab1eb4c7c17"`);
        await queryRunner.query(`ALTER TABLE "course_editors_user" DROP CONSTRAINT "FK_f0f27df7244631f69b3aa42a0dd"`);
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" DROP CONSTRAINT "FK_22ec2de78ee0f0e2e7ee768a4c7"`);
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" DROP CONSTRAINT "FK_6bc76308984eee4bb9c49d9b2a7"`);
        await queryRunner.query(`ALTER TABLE "lecture_editors_user" DROP CONSTRAINT "FK_88ce9c7b7ef0b39989593ab1505"`);
        await queryRunner.query(`ALTER TABLE "lecture_editors_user" DROP CONSTRAINT "FK_a11c650256fe110cdaa8be2579c"`);
        await queryRunner.query(`ALTER TABLE "post_editors_user" DROP CONSTRAINT "FK_4abfcfa0b8814c3522eb488636a"`);
        await queryRunner.query(`ALTER TABLE "post_editors_user" DROP CONSTRAINT "FK_9fc34bb51d7a8a50a875dbeff29"`);
        await queryRunner.query(`ALTER TABLE "game" DROP CONSTRAINT "FK_678fcc30dbaf1c4c7e86bc10d16"`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29"`);
        await queryRunner.query(`ALTER TABLE "course" DROP CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP CONSTRAINT "FK_e61feac3d2667f7f077955c2891"`);
        await queryRunner.query(`ALTER TABLE "post" DROP CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c"`);
        await queryRunner.query(`ALTER TABLE "game" DROP COLUMN "owner_id"`);
        await queryRunner.query(`ALTER TABLE "game" ADD "owner_id" jsonb`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "owner_id"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "owner_id"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "owner_id"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "owner_id"`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "owner_id"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_2469607eeb33fa61f32bda5326"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_d5efb3b9d5fb4cd25e2f1eb849"`);
        await queryRunner.query(`DROP TABLE "proposal_editors_user"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_25fff6cb34e063caab1eb4c7c1"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_f0f27df7244631f69b3aa42a0d"`);
        await queryRunner.query(`DROP TABLE "course_editors_user"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_22ec2de78ee0f0e2e7ee768a4c"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_6bc76308984eee4bb9c49d9b2a"`);
        await queryRunner.query(`DROP TABLE "chapter_editors_user"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_88ce9c7b7ef0b39989593ab150"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_a11c650256fe110cdaa8be2579"`);
        await queryRunner.query(`DROP TABLE "lecture_editors_user"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4abfcfa0b8814c3522eb488636"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_9fc34bb51d7a8a50a875dbeff2"`);
        await queryRunner.query(`DROP TABLE "post_editors_user"`);
        await queryRunner.query(`ALTER TABLE "game" RENAME COLUMN "owner_id" TO "roles"`);
    }

}
