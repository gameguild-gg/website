import { ProposalInterestForm } from '@/components/proposals/proposal-interest-form';
import { getPublishedProposal } from '@/lib/proposals/actions';
import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import React from 'react';

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const proposal = await getPublishedProposal(slug);
  if (!proposal) {
    return { title: 'Course proposal not found' };
  }

  return {
    title: `${proposal.courseTitle} — Course proposal`,
    description: proposal.pitch?.slice(0, 200) ?? `Course proposal: ${proposal.courseTitle}`,
  };
}

/**
 * Public course-proposal detail page (issue #105): renders a published proposal
 * by slug and collects demand signals through the marketing-lead pipeline.
 */
export default async function ProposalDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<React.JSX.Element> {
  const { slug } = await params;
  const proposal = await getPublishedProposal(slug);

  if (!proposal) {
    notFound();
  }

  return (
    <main className="mx-auto w-full max-w-4xl space-y-10 px-4 py-16 sm:px-6">
      <header className="space-y-4">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Course proposal</p>
        <h1 className="text-4xl font-semibold tracking-tight">{proposal.courseTitle}</h1>
        <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
          {proposal.proposerName && <span>Proposed by {proposal.proposerName}</span>}
          {proposal.publishedAt && (
            <time dateTime={proposal.publishedAt}>
              Published {new Date(proposal.publishedAt).toLocaleDateString()}
            </time>
          )}
        </div>
      </header>

      {proposal.pitch && (
        <section aria-labelledby="pitch-heading" className="space-y-3">
          <h2 id="pitch-heading" className="text-2xl font-semibold tracking-tight">
            What this course covers
          </h2>
          <p className="whitespace-pre-line text-base leading-7 text-muted-foreground">{proposal.pitch}</p>
        </section>
      )}

      {proposal.audience && (
        <section aria-labelledby="audience-heading" className="space-y-3">
          <h2 id="audience-heading" className="text-2xl font-semibold tracking-tight">
            Who it is for
          </h2>
          <p className="whitespace-pre-line text-base leading-7 text-muted-foreground">{proposal.audience}</p>
        </section>
      )}

      <ProposalInterestForm courseTitle={proposal.courseTitle} />
    </main>
  );
}
