import Image from 'next/image';
import Link from 'next/link';

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
  // custom fields
  additions: number;
  // custom fields
  deletions: number;
}

export default function ContributorCard(contributor: Contributor) {
  return (
    <div className="bg-white shadow-md rounded-lg overflow-hidden">
      <div className="p-4">
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
        <p className="text-gray-600 text-center mb-4">
          Contributions: {contributor.contributions}
        </p>
        <p className="text-gray-600 text-center mb-4">
          Changes:{' '}
          <span className="text-blue-500 font-bold">
            {contributor.additions + contributor.deletions}
          </span>
          (<span className="text-green-500">+{contributor.additions}</span>/
          <span className="text-red-500">-{contributor.deletions}</span>)
        </p>
        <Link
          href={contributor.url}
          target="_blank"
          rel="noopener noreferrer"
          className="block w-full text-center bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded"
        >
          View Profile
        </Link>
      </div>
    </div>
  );
}
