/**
 * Doctest console-reporter output parser.
 *
 * doctest (https://github.com/doctest/doctest) is the C++ unit-test
 * framework Emception ships in the sysroot for the `doctest` test kind.
 * When invoked without an XML reporter it prints a small, stable
 * grammar to stdout that we parse into structured results so the test
 * runner can:
 *
 *   - report pass/fail counts in the same shape as other test kinds,
 *   - surface individual failed assertions (file, line, expression,
 *     expanded values) without forcing the host to read raw stdout,
 *   - distinguish a clean pass from a test-framework crash.
 *
 * This module is pure string-in / object-out — no I/O, no DOM, no Node
 * APIs — so the same parser runs in the browser worker (where doctest
 * runs under WASM) and in the future Node worker.
 *
 * Sample doctest output we parse (with `--no-colors --no-version`):
 *
 *   src/list_test.cpp:12:
 *   TEST CASE:  push appends
 *
 *   src/list_test.cpp:15: ERROR: CHECK( list.size() == 1 ) is NOT correct!
 *     values: CHECK( 0 == 1 )
 *
 *   ===============================================================================
 *   [doctest] test cases:      2 |      1 passed |      1 failed | 0 skipped
 *   [doctest] assertions:      4 |      3 passed |      1 failed |
 *   [doctest] Status: FAILURE!
 *
 * If the summary lines are missing we treat the run as a crash and
 * return `status: 'crash'` so callers can distinguish a failed test
 * (parsed cleanly, some assertion failed) from a binary that died
 * before doctest could print its summary.
 */

/** Aggregate counts for a single counter type. */
export interface DoctestCounts {
    passed: number;
    failed: number;
    /** Skipped cases. Always 0 for assertions (doctest does not skip them). */
    skipped: number;
    total: number;
}

/** A single failed CHECK / REQUIRE / etc. assertion. */
export interface DoctestFailure {
    /** Owning test case name (parsed from the most recent `TEST CASE:` line). */
    testCase: string;
    /** Source file path as reported by doctest (compiler-relative). */
    file?: string;
    /** 1-based source line of the failing assertion. */
    line?: number;
    /** Macro name, e.g. `CHECK`, `REQUIRE`, `CHECK_EQ`. */
    macro?: string;
    /** Original assertion text, e.g. `CHECK( list.size() == 1 )`. */
    expression: string;
    /** Expanded form with substituted values, when doctest emitted it. */
    expanded?: string;
}

export interface DoctestReport {
    /** 'success' = explicit doctest "Status: SUCCESS!" line. */
    /** 'failure' = explicit doctest "Status: FAILURE!" line. */
    /** 'crash'   = neither summary line found (binary likely died). */
    status: 'success' | 'failure' | 'crash';
    cases: DoctestCounts;
    assertions: DoctestCounts;
    failures: DoctestFailure[];
}

/**
 * Parse the captured stdout of a doctest binary into a structured report.
 *
 * Tolerant of extra application output before/after the doctest blocks —
 * useful when the test binary also prints log lines.
 */
export function parseDoctestConsole(stdout: string): DoctestReport {
    const lines = stdout.split(/\r?\n/);
    const cases = emptyCounts();
    const assertions = emptyCounts();
    const failures: DoctestFailure[] = [];

    let status: DoctestReport['status'] = 'crash';
    let currentTestCase = '';
    /** Failure currently being assembled across consecutive lines. */
    let pending: DoctestFailure | null = null;

    const flushPending = () => {
        if (pending) {
            failures.push(pending);
            pending = null;
        }
    };

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i] ?? '';
        const trimmed = line.trim();

        // --- Summary lines (most authoritative — checked first) ---
        const m1 = trimmed.match(
            /^\[doctest\]\s+test cases:\s*(\d+)\s*\|\s*(\d+)\s+passed\s*\|\s*(\d+)\s+failed\s*\|\s*(\d+)\s+skipped/,
        );
        if (m1) {
            flushPending();
            cases.total = +m1[1];
            cases.passed = +m1[2];
            cases.failed = +m1[3];
            cases.skipped = +m1[4];
            continue;
        }

        const m2 = trimmed.match(
            /^\[doctest\]\s+assertions:\s*(\d+)\s*\|\s*(\d+)\s+passed\s*\|\s*(\d+)\s+failed\s*\|/,
        );
        if (m2) {
            flushPending();
            assertions.total = +m2[1];
            assertions.passed = +m2[2];
            assertions.failed = +m2[3];
            assertions.skipped = 0;
            continue;
        }

        const m3 = trimmed.match(/^\[doctest\]\s+Status:\s+(SUCCESS|FAILURE)!/i);
        if (m3) {
            flushPending();
            status = m3[1].toUpperCase() === 'SUCCESS' ? 'success' : 'failure';
            continue;
        }

        // --- TEST CASE marker (sets context for subsequent failures) ---
        const tc = matchTestCase(trimmed);
        if (tc) {
            flushPending();
            currentTestCase = tc;
            continue;
        }

        // --- Failure line: "<file>:<line>: ERROR: <macro>( ... ) is NOT correct!" ---
        // The file:line prefix is the source location; doctest emits this
        // immediately before the expansion line.
        const err = matchFailureLine(line);
        if (err) {
            flushPending();
            pending = {
                testCase: currentTestCase,
                file: err.file,
                line: err.line,
                macro: err.macro,
                expression: `${err.macro}( ${err.expression} )`,
            };
            continue;
        }

        // --- "values:" continuation line attaches to the pending failure ---
        if (pending) {
            const vals = matchValuesLine(trimmed);
            if (vals) {
                pending.expanded = vals;
                continue;
            }
            // Blank line / separator → finalize the pending failure.
            if (trimmed === '' || isSeparatorLine(trimmed)) {
                flushPending();
                continue;
            }
        }
    }
    flushPending();

    return { status, cases, assertions, failures };
}

function emptyCounts(): DoctestCounts {
    return { passed: 0, failed: 0, skipped: 0, total: 0 };
}

/**
 * Line-oriented matchers for the doctest console grammar. Written as plain
 * string scans instead of backtracking regexes so adversarial application
 * output (e.g. megabyte-long paths or expressions) parses in linear time.
 */

const TEST_CASE_PREFIX = 'TEST CASE:';
const ERROR_MARKER = ': ERROR: ';
const NOT_CORRECT_SUFFIX = ' is NOT correct!';
const VALUES_PREFIX = 'values:';

/** `TEST CASE:  <name>` → `<name>`, else null. */
function matchTestCase(line: string): string | null {
    if (!line.startsWith(TEST_CASE_PREFIX)) return null;
    const name = line.slice(TEST_CASE_PREFIX.length).replace(/^\s+/, '');
    return name.length > 0 ? name : null;
}

/**
 * `<file>:<line>: ERROR: <macro>( <expr> ) is NOT correct!`
 *
 * The file path cannot contain `: ERROR: `, and doctest terminates every
 * failure line with the fixed ` is NOT correct!` suffix, so both anchors are
 * safe string markers rather than ambiguous regex groups.
 *
 * The `<line>` digit run must be *immediately preceded* by the `:` that
 * separates it from the file path. A head like `fileX12` (digits glued to
 * the name, no separating colon) is NOT a line number — the digits are part
 * of the file name. In that case we still match, with `file: 'fileX12'` and
 * no line (the `line` field is optional per `DoctestFailure`).
 */
function matchFailureLine(line: string): {
    file: string;
    line?: number;
    macro: string;
    expression: string;
} | null {
    if (!line.endsWith(NOT_CORRECT_SUFFIX)) return null;
    const errorIndex = line.indexOf(ERROR_MARKER);
    if (errorIndex < 0) return null;

    const head = line.slice(0, errorIndex);
    let numberStart = head.length;
    while (numberStart > 0 && isAsciiDigit(head.charAt(numberStart - 1))) {
        numberStart -= 1;
    }
    let file: string;
    let lineNumber: number | undefined;
    // Only treat the digit run as a line number when a `:` sits immediately
    // in front of it (`<file>:<digits>` head shape).
    if (numberStart < head.length && numberStart > 0 && head.charAt(numberStart - 1) === ':') {
        file = head.slice(0, numberStart - 1);
        lineNumber = Number.parseInt(head.slice(numberStart), 10);
        if (!file || !Number.isFinite(lineNumber)) return null;
    } else {
        file = head;
        if (!file) return null;
    }

    const tail = line.slice(errorIndex + ERROR_MARKER.length, line.length - NOT_CORRECT_SUFFIX.length);
    const openParen = tail.indexOf('(');
    if (openParen < 0) return null;
    const macro = tail.slice(0, openParen).trimEnd();
    if (!/^[A-Z_]+$/.test(macro)) return null;
    let expr = tail.slice(openParen + 1);
    if (!expr.endsWith(')')) return null;
    expr = expr.slice(0, -1).trim();
    if (!expr) return null;

    return { file, line: lineNumber, macro, expression: expr };
}

function isAsciiDigit(char: string): boolean {
    return char >= '0' && char <= '9';
}

/** `values: <expansion>` → `<expansion>`, else null. */
function matchValuesLine(line: string): string | null {
    if (!line.startsWith(VALUES_PREFIX)) return null;
    const expanded = line.slice(VALUES_PREFIX.length).replace(/^\s+/, '');
    return expanded.length > 0 ? expanded : null;
}

/** Separator line made solely of `=` characters. */
function isSeparatorLine(line: string): boolean {
    if (line.length === 0) return false;
    for (let i = 0; i < line.length; i++) {
        if (line.charAt(i) !== '=') return false;
    }
    return true;
}
