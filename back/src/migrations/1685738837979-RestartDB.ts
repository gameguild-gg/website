import { MigrationInterface, QueryRunner } from "typeorm";
import {RenameUserToUserData1685736028991} from "./1685736028991-RenameUserToUserData";
import {InitialDatatypes1685629877979} from "./1685629877979-initial-datatypes";
import {Boilerplate1683865241905} from "./1683865241905-boilerplate";
import {ExtractProfileFromUser1685738837978} from "./1685738837978-ExtractProfileFromUser";

export class RestartDB1685738837979 implements MigrationInterface {
    name = 'RestartDB1685738837979'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await (new ExtractProfileFromUser1685738837978()).down(queryRunner);
        await (new RenameUserToUserData1685736028991()).down(queryRunner);
        await (new InitialDatatypes1685629877979()).down(queryRunner);
        await (new Boilerplate1683865241905()).down(queryRunner);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await (new Boilerplate1683865241905()).up(queryRunner);
        await (new InitialDatatypes1685629877979()).up(queryRunner);
        await (new RenameUserToUserData1685736028991()).up(queryRunner);
        await (new ExtractProfileFromUser1685738837978()).up(queryRunner);
    }
}
