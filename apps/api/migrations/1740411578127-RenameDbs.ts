import { MigrationInterface, QueryRunner } from 'typeorm';

export class RenameDbs1740411578127 implements MigrationInterface {
  name = 'RenameDbs1740411578127';

  public async up(queryRunner: QueryRunner): Promise<void> {
    // rename type job-post_job_type_enum to job_post_job_type_enum
    await queryRunner.query(`ALTER TYPE "job-post_job_type_enum" RENAME TO "job_post_job_type_enum"`);

    // rename job-post_visibility_enum to job_post_visibility_enum
    await queryRunner.query(`ALTER TYPE "job-post_visibility_enum" RENAME TO "job_post_visibility_enum"`);
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    // rename type job_post_job_type_enum to job-post_job_type_enum
    await queryRunner.query(`ALTER TYPE "job_post_job_type_enum" RENAME TO "job-post_job_type_enum"`);

    // rename type job_post_visibility_enum to job-post_visibility_enum
    await queryRunner.query(`ALTER TYPE "job_post_visibility_enum" RENAME TO "job-post_visibility_enum"`);
  }
}
