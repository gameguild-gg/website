import ContributorCard, {
  Contributor,
} from '@/components/contributors/ContributorCard';
import { HealthcheckApi } from '@apiclient/api';
import { Api } from '@apiclient/models';

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
  );

  // get additions and deletions from gitstats and add them to the contributors
  return contributors.map((contributor) => {
    const stats = gitstats.find((stat) => stat.username === contributor.login);
    if (stats) {
      contributor.additions = stats.additions;
      contributor.deletions = stats.deletions;
    }
    return contributor;
  });
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
          <ContributorCard key={contributor.id} {...contributor} />
        ))}
      </div>
    </div>
  );
}
