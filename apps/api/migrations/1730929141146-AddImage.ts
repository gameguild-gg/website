import { MigrationInterface, QueryRunner } from "typeorm";

export class AddImage1730929141146 implements MigrationInterface {
    name = 'AddImage1730929141146'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "images" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "source" character varying NOT NULL, "path" character varying NOT NULL DEFAULT '', "filename" character varying(255) NOT NULL DEFAULT '', "mimetype" character varying(255) NOT NULL DEFAULT '', "size_bytes" integer NOT NULL DEFAULT '0', "hash" character varying NOT NULL, "references" jsonb NOT NULL, "width" integer NOT NULL, "height" integer NOT NULL, "description" character varying, CONSTRAINT "PK_1fe148074c6a1a91b63cb9ee3c9" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_c738d9c7c4c56992bb623c88e2" ON "images" ("source") `);
        await queryRunner.query(`CREATE INDEX "IDX_3fed0dc195b842723edad36ada" ON "images" ("filename") `);
        await queryRunner.query(`CREATE INDEX "IDX_b2900054b2cdd0b2035839335e" ON "images" ("mimetype") `);
        await queryRunner.query(`CREATE INDEX "IDX_11841db41fa6b76f3c6f431ed3" ON "images" ("hash") `);
        await queryRunner.query(`CREATE INDEX "IDX_9cb6c0392eebfa05570c5b3958" ON "images" ("width") `);
        await queryRunner.query(`CREATE INDEX "IDX_afbfec6622f2ac33dc8ee3125f" ON "images" ("height") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "pathUniqueness" ON "images" ("path", "source") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."pathUniqueness"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_afbfec6622f2ac33dc8ee3125f"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_9cb6c0392eebfa05570c5b3958"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_11841db41fa6b76f3c6f431ed3"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_b2900054b2cdd0b2035839335e"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_3fed0dc195b842723edad36ada"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_c738d9c7c4c56992bb623c88e2"`);
        await queryRunner.query(`DROP TABLE "images"`);
    }

}
