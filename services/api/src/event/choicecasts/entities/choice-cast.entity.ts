import { Entity } from "typeorm";
import { EventEntity } from "../../entities/event.entity";

@Entity({ name: "choicecast" })
export class ChoiceCastEntity extends EventEntity {}
