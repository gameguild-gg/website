import ContributorCard, {
  Contributor,
} from '@/components/contributors/ContributorCard';
import { Api, HealthcheckApi } from '@game-guild/apiclient';
import { Github } from 'lucide-react';
import { Metadata, ResolvingMetadata } from 'next';
import Link from 'next/link';
import { Button } from '@/components/ui/button';

// todo: move this logic to the backend and cache the result
async function getContributors(): Promise<Contributor[]> {
  const res = await fetch(
    'https://api.github.com/repos/gameguild-gg/website/contributors',
    { next: { revalidate: 3600 } },
  );

  const api = new HealthcheckApi({ basePath: process.env.NEXT_PUBLIC_API_URL });
  const gitstats = (await api.healthcheckControllerGitstats())
    .body as Api.GitStats[];

  if (!res.ok) throw new Error('Failed to fetch contributors');
  // remove users semantic-release-bot and dependabot[bot] and LMD9977 from contributors
  const contributors = (await res.json()).filter(
    (contributor: Contributor) =>
      contributor.login !== 'semantic-release-bot' &&
      contributor.login !== 'LMD9977' &&
      contributor.login !== 'dependabot[bot]',
  ) as Contributor[];

  const statsRes = await api.healthcheckControllerGitstats();
  if (statsRes.status !== 200) throw new Error('Failed to fetch git stats');
  const stats = statsRes.body;

  // get additions and deletions from gitstats and add them to the contributors
  for (const contributor of contributors) {
    const stat = stats.find((stat) => stat.username === contributor.login);
    if (stat) {
      contributor.additions = stat.additions;
      contributor.deletions = stat.deletions;
    }
  }

  // sort contributors by number of additions and deletions
  contributors.sort(
    (a, b) => b.additions + b.deletions - (a.additions + a.deletions),
  );

  return contributors;
}

// type Props = {
//   params: Promise<{ id: string }>;
//   searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
// };

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: {
      template: '%s | Contributors',
      default: 'Contributors',
    },
    description: 'List of contributors to the Game Guild website',
  };
}

// export async function generateMetadata(
//   // { params, searchParams }: Props,
//   parent: Promise<Metadata>,
// ): Promise<Metadata> {
//   const parentMetadata = await parent;
//
//   return {
//     ...parentMetadata,
//     title: {
//       template: '%s | Contributors',
//       default: 'Contributors',
//     },
//     description: 'List of contributors to the Game Guild website',
//   } as Metadata;
// }

export default async function ContributorsPage() {
  const contributors = await getContributors();

  return (
    <div className="container mx-auto px-4 py-8">
      <h2 className="text-3xl font-bold text-center mb-8 flex items-center justify-center gap-1">
        <Github /> Github Contributors
      </h2>
      <div className="gap-6 justify-center flex">
        <Button>
          <Link
            href="https://github.com/gameguild-gg/website/"
            className="font-bold text-center flex items-center justify-center gap-1"
          >
            <Github /> Github Repo
          </Link>
        </Button>
        <Button>
          <Link href="https://github.com/gameguild-gg/website/stargazers">
            <img
              alt="GitHub Repo stars"
              src="https://img.shields.io/github/stars/gameguild-gg/website?style=social"
            />
          </Link>
        </Button>
        <Button>
          <Link href="https://github.com/gameguild-gg/website/commits/">
            <img
              alt="GitHub Last Commit"
              src="https://img.shields.io/github/last-commit/gameguild-gg/website"
            />
          </Link>
        </Button>
        <Button>
          <Link href="https://status.gameguild.gg/status/gg">
            <img
              alt="Uptime 30d"
              src="https://status.gameguild.gg/api/badge/1/uptime/720?label=Uptime%20(30d)"
            />
          </Link>
        </Button>
        <Button>
          <Link href="https://discord.gg/tac5fZ2bGh">
            <img
              alt="Discord chat"
              src="https://img.shields.io/discord/956922983727915078?logo=discord"
            />
          </Link>
        </Button>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {contributors.map((contributor) => (
          <ContributorCard key={contributor.login} {...contributor} />
        ))}
      </div>
      <h2 className="text-3xl font-bold text-center mb-8 flex items-center justify-center gap-6 mt-8">
        Repo Visualization
      </h2>
      <div className="flex flex-wrap justify-center gap-4">
        <Link href="https://github.com/gameguild-gg/website/stargazers">
          <img
            src="https://api.star-history.com/svg?repos=gameguild-gg/website&type=Date"
            style={{
              width: '30%',
              height: 'auto',
              minWidth: '600px',
              minHeight: '400px',
            }}
            className="w-full max-w-lg mx-auto"
            alt="star history"
          />
        </Link>
        <video
          className="w-full max-w-lg mx-auto"
          src="https://gameguild-gg.github.io/website/gource.mp4"
          controls
          loop
          autoPlay
          muted
        >
          Your browser does not support the video tag.
        </video>
      </div>
      <h2 className="text-3xl font-bold text-center mb-8 flex items-center justify-center gap-6 mt-8">
        OpenCollective Contributors
      </h2>
      <p>
        While we are applying to OpenCollective, we are not able to accept any
        donations. But you can still support us by contributing to our GitHub
        repository, and being part of our community on whatsapp and discord.
      </p>
    </div>
  );
}
