import * as Comlink from "comlink";
import Emception from "./emception";

// Criar uma instância de Emception.
const emception = new Emception();

// Expor emception globalmente.
globalThis.emception = emception;

Comlink.expose(emception);