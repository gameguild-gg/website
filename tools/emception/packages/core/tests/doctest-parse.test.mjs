// Doctest console-output parser verification.

import assert from 'node:assert/strict';
import test from 'node:test';

import { parseDoctestConsole } from '../dist/index.js';

test('clean pass produces success status and zero failures', () => {
    const out = [
        '[doctest] doctest version is "2.4.11"',
        '[doctest] run with "--help" for options',
        '===============================================================================',
        '[doctest] test cases:      3 |      3 passed |      0 failed | 0 skipped',
        '[doctest] assertions:     12 |     12 passed |      0 failed |',
        '[doctest] Status: SUCCESS!',
        '',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.status, 'success');
    assert.deepEqual(r.cases, { passed: 3, failed: 0, skipped: 0, total: 3 });
    assert.deepEqual(r.assertions, { passed: 12, failed: 0, skipped: 0, total: 12 });
    assert.deepEqual(r.failures, []);
});

test('failed CHECK is parsed with file/line/macro/expression/expanded', () => {
    const out = [
        'src/list_test.cpp:12:',
        'TEST CASE:  push appends',
        '',
        'src/list_test.cpp:15: ERROR: CHECK( list.size() == 1 ) is NOT correct!',
        '  values: CHECK( 0 == 1 )',
        '',
        '===============================================================================',
        '[doctest] test cases:      2 |      1 passed |      1 failed | 0 skipped',
        '[doctest] assertions:      4 |      3 passed |      1 failed |',
        '[doctest] Status: FAILURE!',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.status, 'failure');
    assert.equal(r.cases.failed, 1);
    assert.equal(r.assertions.failed, 1);
    assert.equal(r.failures.length, 1);
    assert.deepEqual(r.failures[0], {
        testCase: 'push appends',
        file: 'src/list_test.cpp',
        line: 15,
        macro: 'CHECK',
        expression: 'CHECK( list.size() == 1 )',
        expanded: 'CHECK( 0 == 1 )',
    });
});

test('multiple failures across multiple test cases', () => {
    const out = [
        'TEST CASE:  alpha',
        'a.cpp:1: ERROR: REQUIRE( x ) is NOT correct!',
        '  values: REQUIRE( false )',
        '',
        'TEST CASE:  beta',
        'b.cpp:2: ERROR: CHECK_EQ( a, b ) is NOT correct!',
        '  values: CHECK_EQ( 1, 2 )',
        '',
        '[doctest] test cases:      2 |      0 passed |      2 failed | 0 skipped',
        '[doctest] assertions:      2 |      0 passed |      2 failed |',
        '[doctest] Status: FAILURE!',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.failures.length, 2);
    assert.equal(r.failures[0].testCase, 'alpha');
    assert.equal(r.failures[0].macro, 'REQUIRE');
    assert.equal(r.failures[1].testCase, 'beta');
    assert.equal(r.failures[1].macro, 'CHECK_EQ');
    assert.equal(r.failures[1].expanded, 'CHECK_EQ( 1, 2 )');
});

test('failure without `values:` line still recorded (no expanded field)', () => {
    const out = [
        'TEST CASE:  bare',
        'x.cpp:7: ERROR: CHECK( ready() ) is NOT correct!',
        '',
        '[doctest] test cases:      1 |      0 passed |      1 failed | 0 skipped',
        '[doctest] assertions:      1 |      0 passed |      1 failed |',
        '[doctest] Status: FAILURE!',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.failures.length, 1);
    assert.equal(r.failures[0].expression, 'CHECK( ready() )');
    assert.equal(r.failures[0].expanded, undefined);
});

test('missing summary lines → status is "crash"', () => {
    const out = [
        'TEST CASE:  starts',
        'segfault @ 0xdeadbeef',
        '',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.status, 'crash');
    assert.equal(r.cases.total, 0);
    assert.equal(r.assertions.total, 0);
});

test('skipped count is captured', () => {
    const out = [
        '[doctest] test cases:     10 |      7 passed |      1 failed | 2 skipped',
        '[doctest] assertions:     20 |     19 passed |      1 failed |',
        '[doctest] Status: FAILURE!',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.cases.skipped, 2);
});

test('extra application output between blocks does not confuse parser', () => {
    const out = [
        'app: starting up',
        'TEST CASE:  with logs',
        'app: doing work',
        't.cpp:4: ERROR: CHECK( ok ) is NOT correct!',
        '  values: CHECK( false )',
        'app: shutting down',
        '[doctest] test cases:      1 |      0 passed |      1 failed | 0 skipped',
        '[doctest] assertions:      1 |      0 passed |      1 failed |',
        '[doctest] Status: FAILURE!',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.failures.length, 1);
    assert.equal(r.failures[0].testCase, 'with logs');
    assert.equal(r.failures[0].expanded, 'CHECK( false )');
});

test('empty stdout returns crash + zeroed counts', () => {
    const r = parseDoctestConsole('');
    assert.equal(r.status, 'crash');
    assert.equal(r.cases.total, 0);
    assert.deepEqual(r.failures, []);
});

test('digits glued to the filename are not a line number', () => {
    // No `:<digits>` separator: `fileX12` is the whole file name and the
    // failure must not be misparsed as file='file', line=12.
    const out = [
        'TEST CASE:  glued',
        'fileX12: ERROR: CHECK( x ) is NOT correct!',
        '',
        '[doctest] test cases:      1 |      0 passed |      1 failed | 0 skipped',
        '[doctest] assertions:      1 |      0 passed |      1 failed |',
        '[doctest] Status: FAILURE!',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.failures.length, 1);
    assert.equal(r.failures[0].file, 'fileX12');
    assert.equal(r.failures[0].line, undefined);
    assert.equal(r.failures[0].expression, 'CHECK( x )');
});

test('canonical <file>:<line>: prefix still parses file and line', () => {
    const out = [
        'TEST CASE:  canonical',
        'src/main.ts:42: ERROR: REQUIRE( ok() ) is NOT correct!',
        '',
        '[doctest] test cases:      1 |      0 passed |      1 failed | 0 skipped',
        '[doctest] assertions:      1 |      0 passed |      1 failed |',
        '[doctest] Status: FAILURE!',
    ].join('\n');
    const r = parseDoctestConsole(out);
    assert.equal(r.failures.length, 1);
    assert.equal(r.failures[0].file, 'src/main.ts');
    assert.equal(r.failures[0].line, 42);
});

test('head that is only digits or only a colon is not a file:line prefix', () => {
    const out = [
        'TEST CASE:  degenerate',
        '123: ERROR: CHECK( x ) is NOT correct!',
        '',
    ].join('\n');
    const r = parseDoctestConsole(out);
    // `123` parses as a file name with no line — no crash, no bogus split.
    assert.equal(r.failures.length, 1);
    assert.equal(r.failures[0].file, '123');
    assert.equal(r.failures[0].line, undefined);
});
