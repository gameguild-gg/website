import Image from 'next/image';
import Link from 'next/link';

export const dynamic = 'force-dynamic';

export interface Contributor {
  login: string;
  id: number;
  node_id: string;
  avatar_url: string;
  gravatar_id: string;
  url: string;
  html_url: string;
  followers_url: string;
  following_url: string;
  gists_url: string;
  starred_url: string;
  subscriptions_url: string;
  organizations_url: string;
  repos_url: string;
  events_url: string;
  received_events_url: string;
  type: string;
  user_view_type: string;
  site_admin: boolean;
  contributions: number;
  additions: number;
  deletions: number;
}

// convert the number to a string with K if it is over 1000, and M if it is over 1,000,000 and round to 1 decimal.
function contributionsToString(contributions: number): string {
  if (contributions) {
    if (contributions > 1000000) {
      return `${(contributions / 1000000).toFixed(1)}M`;
    }
    if (contributions > 1000) {
      return `${(contributions / 1000).toFixed(1)}K`;
    }
    return contributions.toFixed(1);
  } else {
    return '0';
  }
}

export default function ContributorCard(contributor: Contributor) {
  if (!contributor) return null;

  return (
    // make all cards clicable to the user's github profile url

    <div className="bg-white shadow-md rounded-lg overflow-hidden">
      <div className="p-4">
        <Link href={contributor.html_url || ''}>
          <Image
            src={contributor.avatar_url}
            alt={`${contributor.login}'s avatar`}
            width={100}
            height={100}
            className="w-24 h-24 rounded-full mx-auto mb-4"
          />
          <h2 className="text-xl font-semibold text-center mb-2">
            {contributor.login}
          </h2>
        </Link>
        <Link
          href={`https://github.com/gameguild-gg/website/commits/main?author=${contributor.login}`}
        >
          <p className="text-gray-600 text-center">
            Contributions: {contributionsToString(contributor.contributions)}
          </p>
          <p className="text-gray-600 text-center">
            LoC:{' '}
            <span className="text-blue-500 font-bold">
              {contributionsToString(
                contributor.additions + contributor.deletions,
              )}
            </span>
            (
            <span className="text-green-500">
              +{contributionsToString(contributor.additions)}
            </span>
            /
            <span className="text-red-500">
              -{contributionsToString(contributor.deletions)}
            </span>
            )
          </p>
          <p className="text-gray-600 text-center">
            {'LoC/Contribs: '}
            <span className="text-blue-500 font-bold">
              {contributionsToString(
                (contributor.additions + contributor.deletions) /
                contributor.contributions,
              )}
            </span>
          </p>
        </Link>
      </div>
    </div>
  );
}
