import type React from 'react';

const FERPA_CONSENT_FORM_URL = 'https://forms.gle/JzMiytNsFWDeBGc4A';

const disclosureRules = [
  'Students choose which education records may be shared with instructors, sponsors, teammates, or reviewers.',
  'A disclosure must describe the recipient, purpose, record type, and expiration date before consent is recorded.',
  'Students can revoke consent for future disclosures without changing historical audit records.',
];

export default async function Page(): Promise<React.JSX.Element> {
  return (
    <article className="flex flex-col gap-8">
      <div className="space-y-3">
        <p className="text-sm font-medium uppercase tracking-[0.18em] text-muted-foreground">Student records</p>
        <h1 className="text-4xl font-bold tracking-tight">FERPA Waiver</h1>
        <p className="text-muted-foreground">
          This waiver explains how GameGuild handles education records in learning, assessment, testing lab, and launch review workflows.
        </p>
      </div>

      <section className="space-y-4 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Consent model</h2>
        <p className="text-sm text-muted-foreground">
          Education records include enrollment status, assessment results, submitted work, attendance, certificate progress, feedback, and project review notes connected to a learner account.
        </p>
        <ul className="space-y-2 text-sm text-muted-foreground">
          {disclosureRules.map((rule) => (
            <li key={rule}>{rule}</li>
          ))}
        </ul>
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Revocation</h2>
        <p className="text-sm text-muted-foreground">
          To revoke a waiver, the student should use account support or the records request workflow. Revocation stops future sharing but does not delete disclosures that were valid at the time they were made.
        </p>
      </section>

      <section className="space-y-4 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Why share coursework publicly?</h2>
        <div className="space-y-3 text-sm text-muted-foreground">
          <p>
            In a typical class, homework and other information delineating academic performance is not visible to the public. Indeed, FERPA requires that students have the right to privacy in this regard. This is one of the main reasons for the existence of so many &quot;walled gardens&quot; for courseware, such as Autolab, Blackboard, Canvas, and Piazza, which keep all student work hidden behind passwords.
          </p>
          <p>
            An essential component of the educational experience is learning how to participate in the &quot;Grand Conversation&quot; all around us by becoming more effective culture operators. Work is strengthened and sharpened in the forge of public scrutiny: in this case, the agora of the Internet.
          </p>
          <p>
            Sometimes students are afraid to publish something because it is of poor quality and worry about embarrassing, negative critiques. In fact, negative critique is quite rare. The most common thing that happens when one creates work of poor quality is that it is simply ignored. Being ignored &mdash; not being shunned or derided &mdash; is the fate of mediocre work.
          </p>
          <p>
            On the other hand, if something truly great is published &mdash; and great projects can happen, even in an introductory class &mdash; there is the chance that it may circulate widely on the Internet. A handful of student projects get blogged and receive tens of thousands of views in a week. This can be an absolutely transformative experience for students that cannot be obtained without taking the risk to work publicly. Students get jobs and build careers on the basis of such success.
          </p>
        </div>
      </section>

      <section className="space-y-4 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Working anonymously</h2>
        <div className="space-y-3 text-sm text-muted-foreground">
          <p>
            There are plenty of valid reasons to work anonymously online. Perhaps you are concerned about stalkers or harassment. Perhaps you wish to address themes in your work which might not meet with the approval of your parents or future employers. These are valid considerations, in which case an anonymous identity on GitHub is advised.
          </p>
          <p>
            On course repositories, work is indexed by a public-facing name, generally your first name. If you would prefer something else, please inform the instructor.
          </p>
        </div>
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Consent form</h2>
        <p className="text-sm text-muted-foreground">
          Fill this form if you want to share your work publicly. If you do not fill this form, your work stays private:
        </p>
        <a
          className="inline-flex w-fit items-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          href={FERPA_CONSENT_FORM_URL}
          rel="noopener noreferrer"
          target="_blank"
        >
          FERPA consent form
        </a>
        <p className="text-xs text-muted-foreground">
          This guidance is a modified version of this{' '}
          <a
            className="underline underline-offset-4 hover:text-foreground"
            href="https://github.com/golanlevin/ExperimentalCapture/blob/master/docs/ferpa.md"
            rel="noopener noreferrer"
            target="_blank"
          >
            original
          </a>
          .
        </p>
      </section>
    </article>
  );
}
