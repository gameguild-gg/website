import assert from 'node:assert/strict';
import test from 'node:test';
import { compareGeneratedContracts, commonPrefixes } from './common-generated-contract-audit.mjs';

const moduleSource = 'export class AuthModule { result(): Types.User { return Types.UserSchema.parse({}); } }';

test('follows referenced schema dependencies and ignores unrelated product-only types', () => {
  const left = { modules: { 'auth.gen.ts': moduleSource }, types: 'export interface User { role: Role }; export type Role = "admin"; export const UserSchema = UserShape; const UserShape = {}; export interface Property { address: string }' };
  const right = { ...left, types: left.types.replace('interface Property { address: string }', 'interface Course { title: string }') };
  assert.equal(compareGeneratedContracts(left, right, ['auth']).passed, true);
  right.types = right.types.replace('"admin"', '"guest"');
  const result = compareGeneratedContracts(left, right, ['auth']);
  assert.equal(result.passed, false);
  assert.ok(result.findings.some(f => f.kind === 'type' && f.name === 'Role' && f.status === 'different'));
});

test('reports one-sided common operations without copying product-specific SDK modules', () => {
  const left = { modules: { 'auth.gen.ts': moduleSource }, types: 'export interface User {} export const UserSchema = {};' };
  const right = { ...left, modules: { ...left.modules, 'auth-step-up.gen.ts': 'export class StepUp {}', 'learning-courses.gen.ts': 'export class Courses {}' } };
  const result = compareGeneratedContracts(left, right, ['auth']);
  assert.equal(result.passed, false);
  assert.equal(result.moduleFiles, 2);
  assert.deepEqual(result.findings.map(f => [f.name, f.status]), [['auth-step-up.gen.ts', 'onlyGameGuild']]);
});

test('normalizes branding only, retaining changed export names and literal values', () => {
  const left = { modules: { 'auth.gen.ts': '// ModuEstate\n' + moduleSource }, types: 'export interface User { value: "ModuEstate" } export const UserSchema = {};' };
  const right = { modules: { 'auth.gen.ts': '// GameGuild\n' + moduleSource }, types: left.types.replace('ModuEstate', 'GameGuild') };
  assert.equal(compareGeneratedContracts(left, right, ['auth']).passed, true);
  right.types = right.types.replace('GameGuild', 'different');
  assert.equal(compareGeneratedContracts(left, right, ['auth']).passed, false);
});

test('does not hide const/let or export changes in referenced runtime schemas', () => {
  const left = { modules: { 'auth.gen.ts': moduleSource }, types: 'export interface User {}\nexport const UserSchema = {};' };
  const right = { ...left, types: left.types.replace('const UserSchema', 'let UserSchema') };
  assert.equal(compareGeneratedContracts(left, right, ['auth']).passed, false);
  right.types = left.types.replace('export const UserSchema', 'const UserSchema');
  assert.equal(compareGeneratedContracts(left, right, ['auth']).passed, false);
});

test('unresolved type references, malformed generated code and unclassified modules cannot pass', () => {
  const missing = { modules: { 'auth.gen.ts': moduleSource }, types: '' };
  assert.equal(compareGeneratedContracts(missing, missing, ['auth']).passed, false);
  assert.throws(() => compareGeneratedContracts({ ...missing, types: 'export interface {' }, missing, ['auth']), /Invalid TypeScript/);
  assert.throws(() => commonPrefixes(['UnknownModule']), /Unclassified/);
  assert.throws(() => compareGeneratedContracts({ modules: {}, types: '' }, { modules: {}, types: '' }, ['auth']), /empty/i);
});
