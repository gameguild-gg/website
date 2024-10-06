import * as Comlink from 'comlink';
import Emception from './emception';

console.log('hello from worker'); // this is not being printed

// Create an instance of Emception.
const emception = new Emception();

// Expose emception globally.
globalThis.emception = emception;

Comlink.expose(emception);

module.exports = emception;
