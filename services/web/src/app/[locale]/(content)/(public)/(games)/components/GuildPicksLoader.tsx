import { Suspense } from "react"
import GuildPicks from "./GuildPicks"
import { getGuildPicks } from "../actions"


export default async function GuildPicksLoader() {
  try {
    const picks = await getGuildPicks()

    return (
      <Suspense fallback={<div>Loading Guild Picks...</div>}>
        <GuildPicks picks={picks} />
      </Suspense>
    )
  } catch (error) {
    console.error("Error loading Guild Picks:", error)
    return <div>Failed to load Guild Picks. Please try again later.</div>
  }
}

