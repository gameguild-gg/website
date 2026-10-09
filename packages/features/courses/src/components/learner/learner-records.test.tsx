import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import {
  LearnerCalendar,
  LearnerCertificates,
  LearnerGradebook,
} from "./learner-records";

const records = [
  {
    course: {
      id: "course-1",
      title: "Game Production",
      slug: "game-production",
      description: "",
      thumbnail: null,
      modules: [],
      overallProgress: 50,
      totalItems: 2,
      completedItems: 1,
      remainingMinutes: 30,
      enrollmentId: "enrollment-1",
    },
    context: {
      enrollmentId: "enrollment-1",
      cohort: { id: "cohort-1", name: "Evening cohort" },
      assessmentGroups: [
        {
          id: "group-1",
          name: "Final project",
          description: "Capstone delivery",
          weightPercent: 40,
          order: 1,
        },
      ],
      discussions: [],
      certificates: [],
      calendar: [
        {
          itemId: "class-1",
          title: "Live critique",
          startsAt: "2026-08-03T22:00:00Z",
          endsAt: "2026-08-03T23:00:00Z",
          itemType: "LiveSession",
        },
      ],
      assessments: [
        {
          id: "assessment-1",
          title: "Playable build",
          maxScore: 100,
          dueAt: "2026-08-05T22:00:00Z",
          assessmentGroupId: "group-1",
          assessmentGroupName: "Final project",
        },
      ],
      submissions: [
        {
          id: "submission-1",
          assessmentId: "assessment-1",
          status: "Graded",
          score: 88,
          passed: true,
          feedback: "Strong iteration and testing evidence.",
        },
      ],
    },
  },
];

describe("learner record views", () => {
  it("renders cohort events and assessment deadlines in one calendar", () => {
    render(<LearnerCalendar records={records} locale="en-US" />);

    expect(screen.getByText("Live critique")).toBeInTheDocument();
    expect(screen.getByText("Playable build")).toBeInTheDocument();
    expect(screen.getByText(/Evening cohort/)).toBeInTheDocument();
    expect(screen.getByText(/timezone/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Month" }));
    expect(
      screen.getByRole("grid", { name: "August 2026" }),
    ).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Event type"), {
      target: { value: "Deadline" },
    });
    expect(screen.queryByText("Live critique")).not.toBeInTheDocument();
    expect(screen.getByText("Playable build")).toBeInTheDocument();
  });

  it("renders grades and instructor feedback from submissions", () => {
    render(<LearnerGradebook records={records} />);

    expect(screen.getByText("88 / 100")).toBeInTheDocument();
    expect(
      screen.getByText("Strong iteration and testing evidence."),
    ).toBeInTheDocument();
    expect(screen.getAllByText("Final project")).toHaveLength(2);
    expect(screen.getByText("40% of final grade")).toBeInTheDocument();
    expect(screen.getByText("35.2 points contributed")).toBeInTheDocument();
  });

  it("keeps assessment rows visible when a grade summary is available", () => {
    render(
      <LearnerGradebook
        records={[
          {
            ...records[0]!,
            context: {
              ...records[0]!.context,
              gradeSummary: {
                finalGrade: 88,
                gradedAssessments: 1,
                totalAssessments: 1,
                earnedPoints: 88,
                possiblePoints: 100,
                percentage: 88,
              },
            },
          },
        ]}
      />,
    );

    expect(screen.getByText("1 of 1 assessments graded")).toBeInTheDocument();
    expect(screen.getByText("Playable build")).toBeInTheDocument();
    expect(screen.getByText("88 / 100")).toBeInTheDocument();
    expect(
      screen.getByText("Strong iteration and testing evidence."),
    ).toBeInTheDocument();
  });

  it("distinguishes a finalized result that has not been released", () => {
    render(
      <LearnerGradebook
        records={[
          {
            ...records[0]!,
            context: {
              ...records[0]!.context,
              submissions: [
                {
                  ...records[0]!.context.submissions[0]!,
                  score: null,
                  passed: null,
                  feedback: null,
                  status: "Graded",
                },
              ],
            },
          },
        ]}
      />,
    );

    expect(screen.getByText("Graded, awaiting release")).toBeInTheDocument();
    expect(screen.getByText("Awaiting release")).toBeInTheDocument();
    expect(screen.getByText("Awaiting results")).toBeInTheDocument();
    expect(screen.queryByText("Awaiting grade")).not.toBeInTheDocument();
  });

  it("renders issued credentials and an honest empty state", () => {
    const { rerender } = render(<LearnerCertificates certificates={[]} />);

    expect(screen.getByText("No certificates issued yet")).toBeInTheDocument();
    rerender(
      <LearnerCertificates
        certificates={[
          {
            id: "certificate-1",
            courseId: "course-1",
            courseName: "Game Production",
            certificateNumber: "GG-2026-001",
            issuedAt: "2026-08-10T12:00:00Z",
            status: "Active",
            verificationUrl: "https://gameguild.gg/certificates/GG-2026-001",
          },
        ]}
      />,
    );
    expect(screen.getByText("GG-2026-001")).toBeInTheDocument();
    expect(screen.getByText("Game Production")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Verify" })).toHaveAttribute(
      "href",
      "https://gameguild.gg/certificates/GG-2026-001",
    );
    expect(screen.getByRole("link", { name: "Download" })).toHaveAttribute(
      "download",
      "game-production-certificate.html",
    );
    expect(screen.getByRole("button", { name: "Share" })).toBeInTheDocument();
  });

  it("renders aggregate grade summaries without detailed assessment rows", () => {
    render(
      <LearnerGradebook
        records={[
          {
            ...records[0]!,
            context: {
              ...records[0]!.context,
              assessments: [],
              submissions: [],
              gradeSummary: {
                finalGrade: 88,
                gradedAssessments: 4,
                totalAssessments: 5,
                earnedPoints: 352,
                possiblePoints: 400,
                percentage: 88,
              },
            },
          },
        ]}
      />,
    );

    expect(screen.getByText("4 of 5 assessments graded")).toBeInTheDocument();
    expect(screen.getAllByText("88%")).toHaveLength(2);
    expect(screen.getByText("4")).toBeInTheDocument();
    expect(screen.getByText("1")).toBeInTheDocument();
  });
});
