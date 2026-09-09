export default function TestingLabLoading() {
  return (
    <div className="space-y-6 p-4 lg:p-6" aria-busy="true" aria-label="Loading Testing Lab">
      <div className="h-14 w-full animate-pulse rounded-md bg-muted" />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => (
          <div key={index} className="h-28 animate-pulse rounded-md border bg-muted/40" />
        ))}
      </div>
      <div className="h-80 animate-pulse rounded-md border bg-muted/30" />
    </div>
  );
}
