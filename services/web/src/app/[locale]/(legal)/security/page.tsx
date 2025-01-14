import React from 'react';

export default async function Page() {
  return (
    <div>
      <h1>Security Policy</h1>

      <h2>Introduction</h2>
      <p>
          The GameGuild is committed to ensuring the security and integrity of our users' data. This security policy outlines our 
          procedures for handling security vulnerabilities and our disclosure policy.
      </p>

      <h2>Reporting Security Vulnerabilities</h2>
      <p>
          If you discover a security vulnerability in the Postiz app, please report it to us privately via email to one of the 
          maintainers:
      </p>
      <ul>
          <li>@tolstenko (<a href="mailto:tolstenko@gmail.com">email</a>)</li>
      </ul>
      <p>
          When reporting a security vulnerability, please provide as much detail as possible, including:
      </p>
      <ul>
          <li>A clear description of the vulnerability</li>
          <li>Steps to reproduce the vulnerability</li>
          <li>Any relevant code or configuration files</li>
      </ul>

      <h2>Supported Versions</h2>
      <p>
          This project currently only supports the latest release. We recommend that users always use the latest version of the 
          Postiz app to ensure they have the latest security patches.
      </p>

      <h2>Disclosure Guidelines</h2>
      <p>
          We follow a private disclosure policy. If you discover a security vulnerability, please report it to us privately via 
          email to one of the maintainers listed above. We will respond promptly to reports of vulnerabilities and work to resolve 
          them as quickly as possible.
      </p>
      <p>
          We will not publicly disclose security vulnerabilities until a patch or fix is available to prevent malicious actors from 
          exploiting the vulnerability before a fix is released.
      </p>

      <h2>Security Vulnerability Response Process</h2>
      <p>We take security vulnerabilities seriously and will respond promptly to reports of vulnerabilities. Our response process includes:</p>
      <ul>
          <li>Investigating the report and verifying the vulnerability.</li>
          <li>Developing a patch or fix for the vulnerability.</li>
          <li>Releasing the patch or fix as soon as possible.</li>
          <li>Notifying users of the vulnerability and the patch or fix.</li>
      </ul>

      <h2>Template Attribution</h2>
      <p>
          This SECURITY.md file is based on the 
          <a href="https://docs.github.com/en/code-security/getting-started/adding-a-security-policy-to-your-repository">
              GitHub Security Policy Template
          </a>.
      </p>

      <p>Thank you for helping to keep the <code>Game Guild</code> secure!</p>
    </div>
  );
}
