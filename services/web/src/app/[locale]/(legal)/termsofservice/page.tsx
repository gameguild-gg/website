import React from 'react';
import {ParamsWithLocale} from "@/types";

export async function generateStaticParams(): Promise<ParamsWithLocale[]> {
  return [];
}

export default async function Page() {
  return (
    <div>
      <h1>Terms of Service</h1>

      <h2>1. Introduction</h2>
      <p>
          Welcome to our platform! By accessing or using our platform, which includes educational content, blogs, and game testing tools 
          (the "Service"), you agree to abide by these Terms of Service ("Terms"). If you do not agree, please do not use our Service.
      </p>

      <h2>2. Data Collection</h2>
      <p>
          To provide the best possible experience, we collect certain data, including but not limited to:
      </p>
      <ul>
          <li>Personal information (e.g., name, email address) during account creation.</li>
          <li>Usage data (e.g., pages visited, time spent on the platform).</li>
          <li>Feedback from game testing activities.</li>
      </ul>
      <p>
          Data is collected in compliance with privacy regulations and will not be shared with third parties without your consent, except 
          as required by law. For more details, refer to our <a href="/privacy-policy.html">Privacy Policy</a>.
      </p>

      <h2>3. User Responsibilities</h2>
      <p>
          By using the Service, you agree to:
      </p>
      <ul>
          <li>Provide accurate and up-to-date information during registration.</li>
          <li>Use the Service only for lawful purposes and in a manner consistent with these Terms.</li>
          <li>Respect the intellectual property rights of others and not engage in unauthorized sharing of content.</li>
      </ul>

      <h2>4. Intellectual Property</h2>
      <p>
          All content on the platform, unless otherwise stated, is licensed under the 
          <a href="https://www.gnu.org/licenses/agpl-3.0.en.html" target="_blank">GNU Affero General Public License v3.0 (AGPLv3)</a>. 
          You are free to use, modify, and redistribute the content in compliance with the AGPLv3 license terms. Attribution is required 
          for any redistributions or derivative works.
      </p>

      <h2>5. Disclaimer of Warranties</h2>
      <p>
          The Service is provided "as is" and "as available," without warranties of any kind, either express or implied, including but 
          not limited to implied warranties of merchantability, fitness for a particular purpose, or non-infringement.
      </p>

      <h2>6. Limitation of Liability</h2>
      <p>
          To the maximum extent permitted by law, we shall not be liable for any indirect, incidental, special, consequential, or 
          punitive damages, including but not limited to loss of profits, data, use, or goodwill arising from or related to your use 
          of the Service.
      </p>

      <h2>7. Changes to These Terms</h2>
      <p>
          We reserve the right to update or modify these Terms at any time. Changes will be effective immediately upon posting. Your 
          continued use of the Service constitutes acceptance of the revised Terms.
      </p>

      <h2>8. Contact Us</h2>
      <p>
          If you have any questions about these Terms, please contact us at 
          <a href="https://discord.gg/QtRTgYRm">Discord</a>, by user @tolstenko
      </p>

      <h2>9. Governing Law</h2>
      <p>
          These Terms are governed by and construed in accordance with the laws of [Your Country/State], without regard to its conflict 
          of law provisions.
      </p>

      <footer>
          <p>
              © [Your Platform Name]. Licensed under 
              <a href="https://www.gnu.org/licenses/agpl-3.0.en.html" target="_blank">AGPLv3</a>.
          </p>
      </footer>
    </div>
  );
}
