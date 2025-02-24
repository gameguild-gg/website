// app/code-of-conduct/page.tsx
import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';
import { MarkdownContent } from '@/components/markdown-content';

const markdownContent: string = `
# Security

---

## Introduction

The GameGuild is committed to ensuring the security and integrity of our users' data. This security policy outlines our procedures for handling security vulnerabilities and our disclosure policy.

---

## Reporting Security Vulnerabilities

If you discover a security vulnerability in the Postiz app, please report it to us privately via email to one of the maintainers:

- [**Discord**](https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg), by user @tolstenko

When reporting a security vulnerability, please provide as much detail as possible, including:

- A clear description of the vulnerability
- Steps to reproduce the vulnerability
- Any relevant code or configuration files

---

## Supported Versions

This project currently only supports the latest release. We recommend that users always use the latest version of the Postiz app to ensure they have the latest security patches.

---

## Disclosure Guidelines

We follow a private disclosure policy. If you discover a security vulnerability, please report it to us privately via email to one of the maintainers listed above. We will:

- Respond promptly to reports of vulnerabilities.
- Work to resolve them as quickly as possible.

We will not publicly disclose security vulnerabilities until a patch or fix is available to prevent malicious actors from exploiting the vulnerability before a fix is released.

---

## Security Vulnerability Response Process

We take security vulnerabilities seriously and will respond promptly to reports of vulnerabilities. Our response process includes:

1. Investigating the report and verifying the vulnerability.
2. Developing a patch or fix for the vulnerability.
3. Releasing the patch or fix as soon as possible.
4. Notifying users of the vulnerability and the patch or fix.

---

## Template Attribution

This SECURITY.md file is based on the [GitHub Security Policy Template](https://docs.github.com/en/code-security/getting-started/adding-a-security-policy-to-your-repository).

---

Thank you for helping to keep the **Game Guild** secure!

`;

export default function Page(): JSX.Element {
  return (
    <div className="prose prose-lg max-w-none mx-auto px-4 py-6 prose-headings:text-blue-600 prose-a:text-blue-500 hover:prose-a:underline prose-strong:text-gray-800">
      <MarkdownContent content={markdownContent} />
    </div>
  );
}
