import { Link } from "@/i18n/navigation";
import Image from "next/image";

/**
 * The GameGuild brand lockup: white tile, four-color mark, and wordmark,
 * linking home. Reusable across the auth screens, public site, and shells.
 */
export function GameGuildLogo({
  locale = "en-US",
  href,
  className = "",
}: {
  locale?: string;
  href?: string;
  className?: string;
}) {
  return (
    <Link
      href={href ?? `/${locale}`}
      className={`flex items-center gap-3 font-semibold ${className}`}
    >
      <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-white text-slate-950">
        <Image
          src="/assets/brand/gameguild-mark.svg"
          alt=""
          width={36}
          height={36}
          unoptimized
          className="size-6"
        />
      </span>
      GameGuild
    </Link>
  );
}
