import {Injectable} from "@nestjs/common";
import {ProposalEntity} from "./proposal.entity";
import { InjectRepository } from '@nestjs/typeorm';
import {Repository} from "typeorm";

@Injectable()
export class ProposalService {
    constructor(
        @InjectRepository(ProposalEntity)
        private repository: Repository<ProposalEntity>,
    ) {}
}