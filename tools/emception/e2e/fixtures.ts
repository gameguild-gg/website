// E2E console error allowlist fixtures — allowed patterns are expected toolchain output
export const ALLOWED_CONSOLE_ERRORS = [
  '[printErr]',      // WASM stderr routed through Emscripten printErr syscall
  '[ninja-bypass]',  // Ninja subprocess bypass mechanism progress/logging
];
