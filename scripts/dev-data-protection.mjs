import { spawnSync } from "node:child_process";
import {
  chmodSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";
import { repositoryRoot } from "./dev-runtime.mjs";

const CERTIFICATE_ENV = "DATAPROTECTION_CERTIFICATE_BASE64";
const KEY_ENV = "DATAPROTECTION_CERTIFICATE_KEY_BASE64";
const DEFAULT_CERTIFICATE_DIRECTORY = path.join(
  repositoryRoot,
  ".cache",
  "gameguild",
  "data-protection",
);

function hasValue(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function runOpenSsl(args) {
  const result = spawnSync("openssl", args, {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  });

  if (result.error?.code === "ENOENT") {
    throw new Error(
      "OpenSSL is required to create the local Data Protection certificate. Install OpenSSL or configure both DATAPROTECTION_CERTIFICATE_* variables in .env.",
    );
  }
  if (result.error) throw result.error;
  return result;
}

function validateExistingCertificatePair(
  certificatePath,
  keyPath,
  executeOpenSsl,
) {
  const certificateCheck = executeOpenSsl([
    "x509",
    "-in",
    certificatePath,
    "-noout",
  ]);
  const keyCheck = executeOpenSsl(["pkey", "-in", keyPath, "-check", "-noout"]);
  if (certificateCheck.status !== 0 || keyCheck.status !== 0) {
    throw new Error(
      "The existing local Data Protection certificate pair is invalid. It was not replaced because persisted keys may still require it for decryption.",
    );
  }

  const certificatePublicKey = executeOpenSsl([
    "x509",
    "-in",
    certificatePath,
    "-pubkey",
    "-noout",
  ]);
  const privateKeyPublicKey = executeOpenSsl([
    "pkey",
    "-in",
    keyPath,
    "-pubout",
  ]);

  if (
    certificatePublicKey.status !== 0 ||
    privateKeyPublicKey.status !== 0 ||
    certificatePublicKey.stdout.trim() !== privateKeyPublicKey.stdout.trim()
  ) {
    throw new Error(
      "The existing local Data Protection certificate and private key do not match. They were not replaced because persisted keys may still require the original certificate.",
    );
  }

  const expirationCheck = executeOpenSsl([
    "x509",
    "-in",
    certificatePath,
    "-noout",
    "-checkend",
    "604800",
  ]);
  if (expirationCheck.status !== 0) {
    throw new Error(
      "The local Data Protection certificate expires within seven days. Automatic replacement is disabled because persisted keys require the existing certificate; rotate it explicitly while retaining historical decryption material.",
    );
  }
}

function generateCertificatePair(
  certificateDirectory,
  certificatePath,
  keyPath,
  executeOpenSsl,
) {
  mkdirSync(certificateDirectory, { recursive: true, mode: 0o700 });
  chmodSync(certificateDirectory, 0o700);

  const temporaryDirectory = mkdtempSync(
    path.join(certificateDirectory, ".generate-"),
  );
  const temporaryCertificatePath = path.join(
    temporaryDirectory,
    "certificate.pem",
  );
  const temporaryKeyPath = path.join(temporaryDirectory, "key.pem");

  try {
    const generated = executeOpenSsl([
      "req",
      "-x509",
      "-newkey",
      "rsa:2048",
      "-noenc",
      "-sha256",
      "-days",
      "3650",
      "-subj",
      "/CN=gameguild-local-development-data-protection",
      "-keyout",
      temporaryKeyPath,
      "-out",
      temporaryCertificatePath,
    ]);
    if (generated.status !== 0) {
      throw new Error(
        `OpenSSL could not create the local Data Protection certificate: ${generated.stderr.trim() || "unknown error"}`,
      );
    }

    writeFileSync(certificatePath, readFileSync(temporaryCertificatePath), {
      mode: 0o600,
    });
    writeFileSync(keyPath, readFileSync(temporaryKeyPath), { mode: 0o600 });
    chmodSync(certificatePath, 0o600);
    chmodSync(keyPath, 0o600);
  } finally {
    rmSync(temporaryDirectory, { recursive: true, force: true });
  }
}

export function resolveDevelopmentDataProtectionEnvironment({
  environment,
  certificateDirectory = DEFAULT_CERTIFICATE_DIRECTORY,
  executeOpenSsl = runOpenSsl,
  log = console.log,
}) {
  const certificateIsConfigured = hasValue(environment[CERTIFICATE_ENV]);
  const keyIsConfigured = hasValue(environment[KEY_ENV]);

  if (certificateIsConfigured !== keyIsConfigured) {
    throw new Error(
      "Both DATAPROTECTION_CERTIFICATE_BASE64 and DATAPROTECTION_CERTIFICATE_KEY_BASE64 must be configured together.",
    );
  }
  if (certificateIsConfigured) return { ...environment };

  const certificatePath = path.join(certificateDirectory, "certificate.pem");
  const keyPath = path.join(certificateDirectory, "key.pem");
  const certificateExists = existsSync(certificatePath);
  const keyExists = existsSync(keyPath);
  if (certificateExists !== keyExists) {
    throw new Error(
      "The local Data Protection certificate pair is incomplete. Existing files were not replaced because persisted keys may still require them for decryption.",
    );
  }
  if (!certificateExists) {
    generateCertificatePair(
      certificateDirectory,
      certificatePath,
      keyPath,
      executeOpenSsl,
    );
    log(
      `[dev] created a persistent local Data Protection certificate in ${path.relative(repositoryRoot, certificateDirectory)}`,
    );
  } else {
    validateExistingCertificatePair(certificatePath, keyPath, executeOpenSsl);
  }

  return {
    ...environment,
    [CERTIFICATE_ENV]: readFileSync(certificatePath).toString("base64"),
    [KEY_ENV]: readFileSync(keyPath).toString("base64"),
  };
}
