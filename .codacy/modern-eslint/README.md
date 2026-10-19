# Modern ESLint source coverage

This hosted-only pass verifies the repository root flat configuration with the
unmodified public Codacy ESLint9 image, pinned by digest. The image contains
ESLint 9.39.5, TypeScript 6.0.3 and typescript-eslint 8.64.0. This avoids parsing
current dependency declarations with the deprecated legacy TypeScript 5.5
runtime. Parser controls include the string export names used by Vite.

The pass records every tracked JavaScript/TypeScript input's actual bytes,
effective rules and parser identity. It observes native wrapper exclusions
separately and force-includes tracked source for the coverage pass, with inline
suppression disabled. The original 241 priority IDs remain in the frozen scope;
all 204 original ESLint issue paths must be present. Other tools need their own
evidence.

The legacy tool and every enabled pattern remain unchanged. Catalog parity
records rule ID availability, including missing IDs; this does not establish
equivalent rule behavior, options, applicability or activation. The root
recommended rules provide additional coverage. They do not replace the 2,631
legacy patterns or certify any original finding as fixed.

An analysis can finish successfully with diagnostics. Source coverage success
must not be interpreted as a clean quality gate. The receipt records both
states separately, and independent collection must qualify source hashes,
before/after markers, complete progress, artifact identity and tool output.
Public artifacts omit diagnostic messages, snippets and fixes.

The first hosted pass processed all 3,283 inputs without parser/configuration
errors or engine exceptions and accepted all 17 controls. It retained 3,661
diagnostics and 150 bytes of unidentified tool output, so complete-analysis
certification remains blocked. Output provenance also accepts source-mapped
dependency and bundled-engine frames and collects a bounded deeper stack;
repository paths, messages and source values remain excluded. Every original
write remains forwarded, and unknown output still fails the evidence gate.

No application dependency, cloud setting, finding state, suppression or
credential is changed by this workflow. Product execution stays in hosted CI.

Both engine workflows use a concurrency group containing the pull request and
source head, with cancellation disabled. A new repair can start its controls
while a prior source's full pass continues. Separate revisions cannot replace
each other's pending analysis. Each analysis checkout explicitly selects that
source head; integration with its destination remains a separate PR check.
