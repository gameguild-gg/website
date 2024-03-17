import Link from 'next/link';

async function NotFound() {
  return (
    <div>
      <h1>404</h1>
      <p>Page not found</p>
      <Link href="/services/web/public">Return to Home</Link>
    </div>
  );
}

export default NotFound;
