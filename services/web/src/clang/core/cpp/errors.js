export class ProcExit extends Error {
  constructor(code) {
    super(`process exited with code ${code}.`);
    this.code = code;
  }
}

export class NotImplemented extends Error {
  constructor(modname, fieldname) {
    super(`${modname}.${fieldname} not implemented.`);
  }
}

export class AbortError extends Error {
  constructor(msg = 'abort') {
    super(msg);
  }
}

export class AssertError extends Error {
  constructor(msg) {
    super(msg);
  }
}
