import ContributorCard, {
  Contributor,
} from '@/components/contributors/ContributorCard';
import { Api, HealthcheckApi } from '@game-guild/apiclient';

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

  // get the stats from gitstats
  const api = new HealthcheckApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
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

export default async function ContributorsPage() {
  const contributors = await getContributors();

  return (
    <div className="container mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold text-center mb-8">
        Github Contributors
      </h1>
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {contributors.map((contributor) => (
          <ContributorCard
            key={contributor.login}
            username={contributor.login}
            profileUrl={contributor.html_url}
            contributions={contributor.contributions}
            additions={contributor.additions}
            deletions={contributor.deletions}
            avatarUrl={contributor.avatar_url}
          />
        ))}
      </div>
    </div>
  );
}
