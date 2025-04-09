import { AssertError } from './errors.js';

export function sleep(ms) {
  return new Promise((resolve, _) => setTimeout(resolve, ms));
}

export function readStr(u8, o, len = -1) {
  let str = '';
  let end = u8.length;
  if (len != -1) end = o + len;
  for (let i = o; i < end && u8[i] != 0; ++i) str += String.fromCharCode(u8[i]);
  return str;
}

export function assert(cond) {
  if (!cond) {
    throw new AssertError('assertion failed.');
  }
}

export function getInstance(module, imports) {
  return WebAssembly.instantiate(module, imports);
}

export function getImportObject(obj, names = []) {
  const result = {};
  for (let name of names) {
    result[name] = obj[name].bind(obj);
  }
  return result;
}

export function msToSec(start, end) {
  return ((end - start) / 1000).toFixed(2);
}

export const ESUCCESS = 0;

export const RAF_PROC_EXIT_CODE = 0xc0c0a;
