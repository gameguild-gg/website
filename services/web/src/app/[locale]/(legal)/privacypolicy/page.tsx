import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';

const markdownContent: string = `
# Privacy Policy

This Privacy Policy explains how we collect, use, and protect your information when you use our platform, including educational tools, blogs, and game testing services (the "Service"). By accessing or using the Service, you agree to the terms outlined in this policy.

---

## 1. Information We Collect

We collect the following types of information:

- **Personal Information:** Information you provide when creating an account, such as your name, email address, and any other details necessary to provide the Service.
- **Usage Data:** Information about your interaction with the Service, including pages visited, time spent on the platform, and actions taken (e.g., submitting feedback or participating in tests).
- **Game Testing Data:** Feedback and gameplay data collected during game tests, which may include performance metrics and user interactions.

---

## 2. How We Use Your Information

Your information is used to:

- Provide, maintain, and improve the Service.
- Personalize your experience and recommend relevant content.
- Analyze platform usage to optimize performance.
- Respond to inquiries or provide customer support.
- Ensure compliance with legal obligations.

---

## 3. Sharing Your Information

We do not sell or share your information with third parties, except in the following circumstances:

- **With Your Consent:** When you explicitly agree to share your information for specific purposes.
- **Legal Obligations:** When required by law, court order, or government regulation.
- **Service Providers:** With trusted third-party service providers who help us operate the platform (e.g., hosting, analytics), under strict confidentiality agreements.

---

## 4. Data Security

We implement robust technical and organizational measures to protect your data from unauthorized access, loss, or misuse. However, no system is completely secure, and we cannot guarantee absolute security.

---

## 5. Your Rights

You have the following rights regarding your personal data:

- **Access:** Request a copy of the personal data we hold about you.
- **Correction:** Request corrections to inaccurate or incomplete data.
- **Deletion:** Request the deletion of your personal data, subject to legal obligations.
- **Objection:** Object to the processing of your data for specific purposes.

To exercise these rights, please contact us at [**Discord**](https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg), by user @tolstenko.

---

## 6. Cookies

Our platform uses cookies to enhance your experience, analyze usage, and provide personalized content. You can manage cookie preferences through your browser settings.

---

## 7. Third-Party Links

The platform may contain links to third-party websites. We are not responsible for the privacy practices or content of these external sites. We encourage you to review their privacy policies before sharing any personal information.

---

## 8. Children’s Privacy

The Service is not directed at children under the age of 13. We do not knowingly collect personal information from children. If we become aware that a child has provided us with personal information, we will take steps to delete it.

---

## 9. Changes to This Privacy Policy

We may update this Privacy Policy from time to time. Changes will be effective upon posting the updated policy on this page. Your continued use of the Service constitutes acceptance of the updated Privacy Policy.

---

## 10. Contact Us

If you have any questions or concerns about this Privacy Policy, please contact us at [**Discord**](https://discord.com/invite/9CdJeQ2XKB?ref=gameguild.gg), by user @tolstenko.

---

© Game Guild. All rights reserved.

Dual Licensed under [AGPLv3](https://www.gnu.org/licenses/agpl-3.0.en.html) and [COMMERCIAL](https://github.com/gameguild-gg/website/blob/main/COMMERCIAL_LICENSE.md)

`;

export default function Page(): JSX.Element {
  return (
    <div className="prose prose-lg max-w-none mx-auto px-4 py-6 prose-headings:text-blue-600 prose-a:text-blue-500 hover:prose-a:underline prose-strong:text-gray-800">
      <MarkdownRenderer content={markdownContent} />
    </div>
  );
}
