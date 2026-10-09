// Tests for kebab↔camel + attribute schema parser
// + DOM event-name registry. Pure module; no DOM/React needed.

import assert from 'node:assert/strict';
import test from 'node:test';

import {
    ATTRIBUTE_SCHEMA,
    EVENT_DOM_NAMES,
    camelToKebab,
    domEventNameFor,
    kebabToCamel,
    parseAttributesToInput,
    parseBooleanAttr,
    parseListAttr,
} from '../dist/index.js';

test('kebabToCamel and camelToKebab are inverses on canonical inputs', () => {
    const cases = [
        ['preset', 'preset'],
        ['manifest-url', 'manifestUrl'],
        ['seed-policy', 'seedPolicy'],
        ['show-hidden', 'showHidden'],
        ['build-url', 'buildUrl'],
        ['include-paths', 'includePaths'],
    ];
    for (const [kebab, camel] of cases) {
        assert.equal(kebabToCamel(kebab), camel, `${kebab} → ${camel}`);
        assert.equal(camelToKebab(camel), kebab, `${camel} → ${kebab}`);
    }
});

test('kebabToCamel is idempotent on already-camel input', () => {
    assert.equal(kebabToCamel('manifestUrl'), 'manifestUrl');
    assert.equal(kebabToCamel(''), '');
});

test('camelToKebab handles single-letter and consecutive caps', () => {
    assert.equal(camelToKebab('a'), 'a');
    assert.equal(camelToKebab('foo'), 'foo');
    // Consecutive caps each become their own dash boundary; this matches
    // the document's expectation for attribute names like "html-uri" rather
    // than "h-t-m-l-uri" — but if a caller really hands us "HTMLUri" we
    // currently emit "-h-t-m-l-uri". Documenting the limitation here so
    // future changes don't silently break parity assumptions.
    assert.equal(camelToKebab('HTMLUri'), '-h-t-m-l-uri');
});

test('parseBooleanAttr matches HTML attribute semantics', () => {
    assert.equal(parseBooleanAttr(null), false);
    assert.equal(parseBooleanAttr(undefined), false);
    assert.equal(parseBooleanAttr(''), true);
    assert.equal(parseBooleanAttr('true'), true);
    assert.equal(parseBooleanAttr('TRUE'), true);
    assert.equal(parseBooleanAttr('1'), true);
    assert.equal(parseBooleanAttr('false'), false);
    assert.equal(parseBooleanAttr('FALSE'), false);
    assert.equal(parseBooleanAttr('0'), false);
    assert.equal(parseBooleanAttr('no'), false);
    assert.equal(parseBooleanAttr('yes'), true);
});

test('parseListAttr splits on whitespace and commas, ignoring empties', () => {
    assert.deepEqual(parseListAttr('-O2 -Wall'), ['-O2', '-Wall']);
    assert.deepEqual(parseListAttr('-O2,-Wall'), ['-O2', '-Wall']);
    assert.deepEqual(parseListAttr('-O2, -Wall   -Werror'), ['-O2', '-Wall', '-Werror']);
    assert.equal(parseListAttr(''), undefined);
    assert.equal(parseListAttr('  '), undefined);
    assert.equal(parseListAttr(null), undefined);
});

test('EVENT_DOM_NAMES covers every event with the emception- prefix', () => {
    for (const [internal, dom] of Object.entries(EVENT_DOM_NAMES)) {
        assert.ok(
            dom.startsWith('emception-'),
            `${internal} → ${dom} should start with 'emception-'`,
        );
        // Internal name (lowercased) should appear as the suffix verbatim.
        assert.equal(dom, `emception-${internal}`);
    }
    // Spec-required events must be present.
    for (const required of ['ready', 'stdout', 'stderr', 'exit', 'test-report', 'test-case']) {
        assert.ok(required in EVENT_DOM_NAMES, `missing required event '${required}'`);
    }
});

test('domEventNameFor returns the canonical DOM name', () => {
    assert.equal(domEventNameFor('ready'), 'emception-ready');
    assert.equal(domEventNameFor('test-case'), 'emception-test-case');
    assert.equal(domEventNameFor('test-report'), 'emception-test-report');
    assert.equal(domEventNameFor('bundle-loaded'), 'emception-bundle-loaded');
});

test('parseAttributesToInput maps top-level kebab attrs', () => {
    const input = parseAttributesToInput({
        'preset': 'cpp',
        'manifest-url': 'https://cdn.example.com/manifest.json',
        'workspace': 'demo',
        'seed-policy': 'overwrite',
        'autorun': '',
        'canvas': 'true',
        'show-hidden': 'false',
    });
    assert.deepEqual(input, {
        preset: 'cpp',
        manifestUrl: 'https://cdn.example.com/manifest.json',
        workspace: 'demo',
        seedPolicy: 'overwrite',
        autorun: true,
        canvas: true,
        showHidden: false,
    });
});

test('parseAttributesToInput folds build attrs into workspace directly', () => {
    const input = parseAttributesToInput({
        'preset': 'cpp',
        'flags': '-O2 -Wall',
        'ldflags': '-lm',
        'output': 'a.out',
        'include-paths': 'inc, vendor/inc',
    });
    assert.deepEqual(input, {
        preset: 'cpp',
        workspace: {
            output: 'a.out',
            flags: ['-O2', '-Wall'],
            ldflags: ['-lm'],
            includePaths: ['inc', 'vendor/inc'],
        },
    });
});

test('parseAttributesToInput rejects unknown enum values', () => {
    assert.throws(
        () => parseAttributesToInput({ 'seed-policy': 'wipe' }),
        /seed-policy.*expected one of.*got 'wipe'/,
    );
});

test('parseAttributesToInput silently ignores unknown attrs by default', () => {
    const input = parseAttributesToInput({ 'preset': 'cpp', 'random-thing': 'xyz' });
    assert.deepEqual(input, { preset: 'cpp' });
});

test('parseAttributesToInput surfaces unknowns via onUnknown callback', () => {
    const seen = [];
    parseAttributesToInput(
        { 'preset': 'cpp', 'random-thing': 'xyz', 'other': 'qq' },
        { onUnknown: (k, v) => seen.push([k, v]) },
    );
    assert.deepEqual(
        seen.sort((a, b) => a[0].localeCompare(b[0])),
        [['other', 'qq'], ['random-thing', 'xyz']],
    );
});

test('parseAttributesToInput skips attrs with undefined / empty string values', () => {
    const input = parseAttributesToInput({
        'preset': 'cpp',
        'output': undefined,
        'manifest-url': '',
    });
    // Empty-string values for non-boolean attrs are treated as "not set".
    assert.deepEqual(input, { preset: 'cpp' });
});

test('parseAttributesToInput schema gate blocks arbitrary attribute names from reaching setPath', () => {
    // The schema gate is the security boundary: any attribute name that
    // isn't in ATTRIBUTE_SCHEMA never reaches setPath, so attacker-supplied
    // dotted paths can't traverse internal objects. Verify by feeding a
    // grab-bag of dangerous-looking keys and confirming they all land in
    // onUnknown without polluting the result or Object.prototype.
    const seen = [];
    const input = parseAttributesToInput(
        {
            'preset': 'cpp',
            'workspace.build.cflags': '-O2',
            'constructor': 'evil',
            'foo.bar': 'qq',
        },
        { onUnknown: (k) => seen.push(k) },
    );
    assert.deepEqual(input, { preset: 'cpp' });
    assert.ok(seen.includes('workspace.build.cflags'));
    assert.ok(seen.includes('constructor'));
    assert.ok(seen.includes('foo.bar'));
    assert.equal({}.evil, undefined);
});

test('ATTRIBUTE_SCHEMA includes all spec-required attrs', () => {
    for (const required of ['flags', 'ldflags', 'output', 'build-url']) {
        assert.ok(required in ATTRIBUTE_SCHEMA, `missing attr '${required}'`);
    }
});

test('build attributes never traverse an inherited workspace object', () => {
    const inheritedWorkspace = {};
    Object.defineProperty(Object.prototype, 'workspace', {
        value: inheritedWorkspace,
        configurable: true,
        writable: true,
    });
    try {
        const input = parseAttributesToInput({ flags: '-O2', output: 'a.out' });
        assert.deepEqual(inheritedWorkspace, {});
        assert.ok(Object.hasOwn(input, 'workspace'));
        assert.deepEqual(input.workspace, { flags: ['-O2'], output: 'a.out' });
    } finally {
        delete Object.prototype.workspace;
    }
});

test('schema extensions cannot assign reserved intermediate or final path segments', () => {
    const key = 'security-regression';
    try {
        for (const target of ['__proto__.polluted', 'constructor.prototype.polluted', 'prototype.polluted', '__proto__', 'constructor', 'prototype']) {
            ATTRIBUTE_SCHEMA[key] = { target, kind: 'string' };
            const input = parseAttributesToInput({ [key]: 'polluted' });
            assert.deepEqual(input, {});
            assert.equal(Object.prototype.polluted, undefined);
            assert.equal(Object.getPrototypeOf(input), Object.prototype);
        }
    } finally {
        delete ATTRIBUTE_SCHEMA[key];
    }
});

test('final write never invokes inherited setters (defineProperty semantics)', () => {
    // setPath writes with Object.defineProperty, so a hostile inherited
    // setter (e.g. planted via earlier prototype pollution) is never called
    // for a final path segment, unlike a plain `obj[key] = value` write.
    const calls = [];
    Object.defineProperty(Object.prototype, 'output', {
        set(v) { calls.push(v); },
        get() { return undefined; },
        configurable: true,
    });
    try {
        const input = parseAttributesToInput({ output: 'a.out' });
        assert.deepEqual(calls, []);
        assert.deepEqual(input, { workspace: { output: 'a.out' } });
        assert.equal(({}).polluted, undefined);
    } finally {
        delete Object.prototype.output;
    }
});
