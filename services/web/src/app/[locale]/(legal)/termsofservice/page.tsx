// app/code-of-conduct/page.tsx
import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';
import { MarkdownContent } from '@/components/markdown-content';

const markdownContent: string = `
# Terms of Service

---

## 1. Introduction

Welcome to our platform! By accessing or using our platform, which includes educational content, blogs, and game testing tools (the "Service"), you agree to abide by these Terms of Service ("Terms"). If you do not agree, please do not use our Service.

---

## 2. Data Collection

To provide the best possible experience, we collect certain data, including but not limited to:

- Personal information (e.g., name, email address) during account creation.
- Usage data (e.g., pages visited, time spent on the platform).
- Feedback from game testing activities.

Data is collected in compliance with privacy regulations and will not be shared with third parties without your consent, except as required by law. For more details, refer to our [Privacy Policy](/privacy-policy.html).

---

## 3. User Responsibilities

By using the Service, you agree to:

- Provide accurate and up-to-date information during registration.
- Use the Service only for lawful purposes and in a manner consistent with these Terms.
- Respect the intellectual property rights of others and not engage in unauthorized sharing of content.

---

## 4. Intellectual Property

All content on the platform, unless otherwise stated, is licensed under the [GNU Affero General Public License v3.0 (AGPLv3)](https://www.gnu.org/licenses/agpl-3.0.en.html). You are free to use, modify, and redistribute the content in compliance with the AGPLv3 license terms. Attribution is required for any redistributions or derivative works.

---

## 5. Disclaimer of Warranties

The Service is provided "as is" and "as available," without warranties of any kind, either express or implied, including but not limited to implied warranties of merchantability, fitness for a particular purpose, or non-infringement.

---

## 6. Limitation of Liability

To the maximum extent permitted by law, we shall not be liable for any indirect, incidental, special, consequential, or punitive damages, including but not limited to loss of profits, data, use, or goodwill arising from or related to your use of the Service.

---

## 7. Changes to These Terms

We reserve the right to update or modify these Terms at any time. Changes will be effective immediately upon posting. Your continued use of the Service constitutes acceptance of the revised Terms.

---

## 8. Contact Us

If you have any questions about these Terms, please contact us at [**Discord**](https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg), by user @tolstenko.

---

## 9. Governing Law

These Terms are governed by and construed in accordance with the laws of [Brazil], without regard to its conflict of law provisions.

---

© [GameGuild](gameguild.gg). Dual Licensed under [AGPLv3](https://www.gnu.org/licenses/agpl-3.0.en.html) and [COMMERCIAL](https://github.com/gameguild-gg/website/blob/main/COMMERCIAL_LICENSE.md)


`;

export default function Page(): JSX.Element {
  return (
    <div className="prose prose-lg max-w-none mx-auto px-4 py-6 prose-headings:text-blue-600 prose-a:text-blue-500 hover:prose-a:underline prose-strong:text-gray-800">
      <MarkdownContent content={markdownContent} />
    </div>
  );
}
