import Image from 'next/image';
import Link from 'next/link';

interface ContributorCardProps {
  username: string;
  avatarUrl: string;
  contributions: number;
  profileUrl: string;
}

export default function ContributorCard({
  username,
  avatarUrl,
  contributions,
  profileUrl,
}: ContributorCardProps) {
  return (
    <div className="bg-white shadow-md rounded-lg overflow-hidden">
      <div className="p-4">
        <Image
          src={avatarUrl}
          alt={`${username}'s avatar`}
          width={100}
          height={100}
          className="w-24 h-24 rounded-full mx-auto mb-4"
        />
        <h2 className="text-xl font-semibold text-center mb-2">{username}</h2>
        <p className="text-gray-600 text-center mb-4">
          Contributions: {contributions}
        </p>
        <Link
          href={profileUrl}
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
