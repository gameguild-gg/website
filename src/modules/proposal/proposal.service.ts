import {Injectable} from "@nestjs/common";
import {ProposalEntity} from "./proposal.entity";
import { InjectRepository } from '@nestjs/typeorm';
import {Repository} from "typeorm";
import {TypeOrmCrudService} from "@dataui/crud-typeorm";

@Injectable()
export class ProposalService extends TypeOrmCrudService<ProposalEntity> {
    constructor(
        @InjectRepository(ProposalEntity)
        private repository: Repository<ProposalEntity>,
    ) {
        super(repository);
    }
}