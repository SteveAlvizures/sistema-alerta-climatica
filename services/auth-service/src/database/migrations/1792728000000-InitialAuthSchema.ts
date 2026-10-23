import type {
  MigrationInterface,
  QueryRunner,
} from 'typeorm';

export class InitialAuthSchema1792728000000
  implements MigrationInterface
{
  name = 'InitialAuthSchema1792728000000';

  public async up(
    queryRunner: QueryRunner,
  ): Promise<void> {
    await queryRunner.query(`
      CREATE EXTENSION IF NOT EXISTS "pgcrypto"
    `);

    await queryRunner.query(`
      CREATE TABLE "permissions" (
        "id" uuid NOT NULL DEFAULT gen_random_uuid(),
        "name" character varying(120) NOT NULL,
        "description" character varying(250),
        CONSTRAINT "PK_permissions" PRIMARY KEY ("id"),
        CONSTRAINT "UQ_permissions_name" UNIQUE ("name")
      )
    `);

    await queryRunner.query(`
      CREATE TABLE "roles" (
        "id" uuid NOT NULL DEFAULT gen_random_uuid(),
        "name" character varying(100) NOT NULL,
        "description" character varying(250),
        CONSTRAINT "PK_roles" PRIMARY KEY ("id"),
        CONSTRAINT "UQ_roles_name" UNIQUE ("name")
      )
    `);

    await queryRunner.query(`
      CREATE TABLE "users" (
        "id" uuid NOT NULL DEFAULT gen_random_uuid(),
        "name" character varying(150) NOT NULL,
        "email" character varying(254) NOT NULL,
        "password_hash" character varying NOT NULL,
        "role_id" uuid NOT NULL,
        "is_active" boolean NOT NULL DEFAULT true,
        "created_at" TIMESTAMP NOT NULL DEFAULT now(),
        "last_access_at" TIMESTAMP WITH TIME ZONE,
        CONSTRAINT "PK_users" PRIMARY KEY ("id"),
        CONSTRAINT "UQ_users_email" UNIQUE ("email"),
        CONSTRAINT "FK_users_role"
          FOREIGN KEY ("role_id")
          REFERENCES "roles"("id")
          ON DELETE NO ACTION
          ON UPDATE NO ACTION
      )
    `);

    await queryRunner.query(`
      CREATE TABLE "role_permissions" (
        "role_id" uuid NOT NULL,
        "permission_id" uuid NOT NULL,
        CONSTRAINT "PK_role_permissions"
          PRIMARY KEY ("role_id", "permission_id"),
        CONSTRAINT "FK_role_permissions_role"
          FOREIGN KEY ("role_id")
          REFERENCES "roles"("id")
          ON DELETE CASCADE
          ON UPDATE CASCADE,
        CONSTRAINT "FK_role_permissions_permission"
          FOREIGN KEY ("permission_id")
          REFERENCES "permissions"("id")
          ON DELETE NO ACTION
          ON UPDATE NO ACTION
      )
    `);

    await queryRunner.query(`
      CREATE INDEX "IDX_role_permissions_role"
      ON "role_permissions" ("role_id")
    `);

    await queryRunner.query(`
      CREATE INDEX "IDX_role_permissions_permission"
      ON "role_permissions" ("permission_id")
    `);
  }

  public async down(
    queryRunner: QueryRunner,
  ): Promise<void> {
    await queryRunner.query(`
      DROP INDEX IF EXISTS "IDX_role_permissions_permission"
    `);

    await queryRunner.query(`
      DROP INDEX IF EXISTS "IDX_role_permissions_role"
    `);

    await queryRunner.query(`
      DROP TABLE IF EXISTS "role_permissions"
    `);

    await queryRunner.query(`
      DROP TABLE IF EXISTS "users"
    `);

    await queryRunner.query(`
      DROP TABLE IF EXISTS "roles"
    `);

    await queryRunner.query(`
      DROP TABLE IF EXISTS "permissions"
    `);
  }
}
