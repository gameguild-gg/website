import type React from 'react';

const standards = [
  'Submit original work or clearly identify borrowed code, assets, writing, and research.',
  'Keep assessment attempts fair by following the stated collaboration and tool-use rules.',
  'Do not impersonate another learner, submit work for another learner, or manipulate progress evidence.',
  'Use AI assistance only when the course or challenge allows it and disclose material AI-generated contributions.',
];

const acceptableAiUses = [
  'On writing, coding, or interactive assignments, you can ask AI questions about concepts, ideas, syntax, and similar topics.',
  'You can ask AI assistants what is wrong with your code, but you cannot paste the answer 1-to-1 into your final submission. You have to modify it.',
  'If your submission contains part of an AI-assisted tool, you have to cite it, for example: "I prompted ____ in ChatGPT, and the answer was ____." Cited AI-assisted submissions are graded with fairness — a point deduction instead of a zero.',
];

const unacceptableAiUses = [
  'You cannot copy the question, prompt an AI to answer it, and use the answer as your own.',
  'You cannot ask AI to code a solution for you.',
  'You cannot use any AI while coding (e.g., GitHub Copilot); use an IDE instead without advanced autocomplete features.',
  'You cannot use AI assistance to solve quizzes or exams under any circumstances.',
  'Even in accepted cases, using AI assistance without citing it is considered plagiarism: it will be reported and zeroed.',
];

const plagiarismDefinitions = [
  'Searching for answers on the internet and copying and pasting them as your own.',
  'Copying answers from other students.',
  'Using AI-assisted tools to produce full answers.',
  'Using AI-assisted tools to produce partial answers without citing them.',
];

export default async function Page(): Promise<React.JSX.Element> {
  return (
    <article className="flex flex-col gap-8">
      <div className="space-y-3">
        <p className="text-sm font-medium uppercase tracking-[0.18em] text-muted-foreground">Learner policy</p>
        <h1 className="text-4xl font-bold tracking-tight">Academic Honesty</h1>
        <p className="text-muted-foreground">
          GameGuild learning, testing, and launch programs depend on honest authorship and reliable assessment evidence.
        </p>
      </div>

      <section className="space-y-4 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Standards</h2>
        <ul className="space-y-2 text-sm text-muted-foreground">
          {standards.map((standard) => (
            <li key={standard}>{standard}</li>
          ))}
        </ul>
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Plagiarism and misuse</h2>
        <p className="text-sm text-muted-foreground">
          Plagiarism is a serious offense and will be reported. Plagiarism, fabricated testing evidence, credential sharing, copied submissions, and undisclosed paid assistance may result in assessment reset, certificate hold, account restriction, or program removal. Plagiarism is defined as:
        </p>
        <ul className="space-y-2 text-sm text-muted-foreground">
          {plagiarismDefinitions.map((definition) => (
            <li key={definition}>{definition}</li>
          ))}
        </ul>
      </section>

      <section className="space-y-4 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Policy on limited use of AI-assisted tools</h2>
        <p className="text-sm text-muted-foreground">
          During classes, AI writing tools such as ChatGPT may be used in certain specific cases. You will be informed as to when, where, and how these tools are permitted to be used, along with guidance for attribution. Any use outside of these specific cases constitutes a violation of the Academic Honesty Policy.{' '}
          <a
            className="underline underline-offset-4 hover:text-foreground"
            href="https://clt.champlain.edu/kb/communicating-your-chatgpt-ai-policies/"
            rel="noopener noreferrer"
            target="_blank"
          >
            Source
          </a>
          .
        </p>
        <p className="text-sm text-muted-foreground">
          Learners have to produce original content. You can use tools like ChatGPT to help you learn by prompting your own questions, but not to solve problems, assignments, or quizzes. The rationale: students have to learn the concepts and ideas rather than just copying and pasting answers.
        </p>
        <div className="space-y-2">
          <h3 className="text-base font-semibold">What is acceptable</h3>
          <ul className="space-y-2 text-sm text-muted-foreground">
            {acceptableAiUses.map((use) => (
              <li key={use}>{use}</li>
            ))}
          </ul>
        </div>
        <div className="space-y-2">
          <h3 className="text-base font-semibold">What is not acceptable</h3>
          <ul className="space-y-2 text-sm text-muted-foreground">
            {unacceptableAiUses.map((use) => (
              <li key={use}>{use}</li>
            ))}
          </ul>
        </div>
        <div className="space-y-2">
          <h3 className="text-base font-semibold">How plagiarism and AI misuse are detected</h3>
          <ul className="space-y-2 text-sm text-muted-foreground">
            <li>Automated tools such as Turnitin (Canvas), MOSS (Beecrowd), and others.</li>
            <li>Instructor experience reviewing submissions.</li>
            <li>If two students use the same AI assistant, chances are high they produce the same answer — and it will be detected.</li>
          </ul>
        </div>
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Grading timings</h2>
        <p className="text-sm text-muted-foreground">
          Assignments are usually graded within 1 week, as soon as possible. The worst-case scenario is two weeks.
        </p>
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Late submissions</h2>
        <div className="space-y-3 text-sm text-muted-foreground">
          <p>
            If you submit an assignment late, you will receive a 1% deduction per day on your grade up to 25%.
          </p>
          <p>
            If you have accommodations, send a message on every submission stating that, and the instructor will try to accommodate you.
          </p>
          <p>
            If you fall under special conditions, such as sickness, death of a relative, or any other condition that prevents you from submitting the assignment on time, send a message and the instructor will try to accommodate you.
          </p>
        </div>
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-5">
        <h2 className="text-xl font-semibold">Welcoming environment</h2>
        <p className="text-sm text-muted-foreground">
          Instructors are here to teach the best they can and guide learners through the learning process. You can count on them as friends and teachers, and they will help you as much as possible. Exceptions are willing to be made for the ones that need it.
        </p>
      </section>
    </article>
  );
}
