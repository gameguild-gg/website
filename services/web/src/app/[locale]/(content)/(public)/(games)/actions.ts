"use server"

export async function getGuildPicks() {
  // Simulating a delay to demonstrate async behavior
  await new Promise((resolve) => setTimeout(resolve, 1000))

  const guildPicks = [
    {
      id: 1,
      title: "The Witcher 3",
      image: "/placeholder.svg?height=400&width=800",
      badge: "GOTY Edition",
      description: "An award-winning action role-playing game set in a vast open world.",
    },
    {
      id: 2,
      title: "PUBG: Battlegrounds",
      image: "/placeholder.svg?height=400&width=800",
      description: "A battle royale shooter that pits 100 players against each other in a struggle for survival.",
    },
    {
      id: 3,
      title: "Cyberpunk 2077",
      image: "/placeholder.svg?height=400&width=800",
      badge: "2.0",
      description:
        "An open-world, action-adventure story set in Night City, a megalopolis obsessed with power, glamour and body modification.",
    },
  ]

  return guildPicks
}

