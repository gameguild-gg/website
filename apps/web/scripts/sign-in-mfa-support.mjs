import { createHmac } from "node:crypto";

/** Independent RFC 6238 SHA-1 generator for disposable browser test accounts. */
export function generateTotpCode(secret, timeMilliseconds = Date.now()) {
  if (typeof secret !== "string" || !/^[A-Z2-7]+={0,6}$/i.test(secret))
    throw new Error("Invalid authenticator setup key");
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  let value = 0,
    bits = 0;
  const bytes = [];
  for (const char of secret.toUpperCase().replace(/=+$/, "")) {
    value = (value << 5) | alphabet.indexOf(char);
    bits += 5;
    if (bits >= 8) {
      bits -= 8;
      bytes.push((value >>> bits) & 255);
    }
  }
  if (!bytes.length) throw new Error("Empty authenticator setup key");
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(timeMilliseconds / 30000)));
  const digest = createHmac("sha1", Buffer.from(bytes))
    .update(counter)
    .digest();
  const offset = digest.at(-1) & 15;
  return String((digest.readUInt32BE(offset) & 0x7fffffff) % 1000000).padStart(
    6,
    "0",
  );
}

/** Keeps challenge setup secrets in process memory only and respects native replay prevention. */
export function createMfaSignInContinuation({
  secrets = new Map(),
  now = Date.now,
  wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
} = {}) {
  const usedCounters = new Map();
  return async function completeMfaSignIn(firstFactor, send, accountKey) {
    if (firstFactor.requiresMfa !== true && firstFactor.error !== "MfaRequired")
      return firstFactor;
    const { mfaToken } = firstFactor;
    if (typeof mfaToken !== "string" || !/^[A-Za-z0-9_-]{43}$/.test(mfaToken))
      throw new Error("Sign-in did not provide a valid limited MFA challenge");
    let secret = secrets.get(accountKey);
    if (!secret) {
      const setup = await send("enrollment", { mfaToken });
      if (setup.success !== true || typeof setup.secretKey !== "string")
        throw new Error("Limited authenticator enrollment did not complete");
      secret = setup.secretKey;
      secrets.set(accountKey, secret);
    }
    let counter = Math.floor(now() / 30000);
    while (counter <= (usedCounters.get(accountKey) ?? -1)) {
      await wait(Math.max(1, (counter + 1) * 30000 - now()));
      counter = Math.floor(now() / 30000);
    }
    const code = generateTotpCode(secret, counter * 30000);
    usedCounters.set(accountKey, counter);
    // The actor and tenant stay bound to the server challenge. Neither is sent here.
    const completed = await send("complete", {
      mfaToken,
      method: "Totp",
      code,
    });
    if (completed.requiresMfa === true || completed.error)
      throw new Error("MFA sign-in did not complete");
    return completed;
  };
}
