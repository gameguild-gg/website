# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| develop | :white_check_mark: |
| < 1.0   | :x:                |

We provide security fixes for the latest release line and the `develop` branch.
Older releases are not maintained.

## Reporting a Vulnerability

Please do **not** report security vulnerabilities through public GitHub issues,
discussions, or pull requests.

Use [GitHub's private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-reviewing/privately-reporting-a-security-vulnerability)
for this repository: **Security → Report a vulnerability**. Reports sent this
way reach the maintainers privately and allow coordinated disclosure.

Please include as much of the following as possible:

- The type of issue (e.g. authentication bypass, injection, data exposure).
- Full paths of source files related to the manifestation.
- The location of the affected source (tag/commit/branch).
- Any special configuration required to reproduce the issue.
- Step-by-step instructions to reproduce the issue.
- Proof-of-concept or exploit code (if possible).
- Impact, including how an attacker might exploit the issue.

You should receive an acknowledgement within 72 hours. If you do not, please
follow up via the same private channel.

## Scope

The following are in scope:

- The API (`apps/api`), web app (`apps/web`), and shared packages shipped by
  this repository.
- Authentication, authorization, and multi-tenant isolation bugs.
- Injection, SSRF, XSS, CSRF, and data-exposure vulnerabilities in our code.

Out of scope:

- Vulnerabilities in third-party services we do not control.
- Automated scanner reports without a demonstrable impact.
- Social engineering, phishing, or physical attacks.
- Denial-of-service via resource exhaustion of public demo environments.

## Preferred Languages

We prefer all communications in English or Portuguese.
