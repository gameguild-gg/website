import ContributorCard from '@/components/contributors/ContributorCard';

interface Contributor {
  login: string;
  avatar_url: string;
  contributions: number;
  html_url: string;
}

async function getContributors(): Promise<Contributor[]> {
  const res = await fetch(
    'https://api.github.com/repos/gameguild-gg/website/contributors',
    { next: { revalidate: 3600 } },
  );
  if (!res.ok) throw new Error('Failed to fetch contributors');
  return res.json();
}

export default async function ContributorsPage() {
  const contributors = await getContributors();

  return (
    <div className="container mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold text-center mb-8">
        GameGuild Code Contributors
      </h1>
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {contributors.map((contributor) => (
          <ContributorCard
            key={contributor.login}
            username={contributor.login}
            avatarUrl={contributor.avatar_url}
            contributions={contributor.contributions}
            profileUrl={contributor.html_url}
          />
        ))}
      </div>
    </div>
  );
}
