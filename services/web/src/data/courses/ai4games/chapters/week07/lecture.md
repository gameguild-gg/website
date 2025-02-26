# Using Metrics, Analytics, A/B Testing, and Machinations

## Introduction

::: example "Zynga"

> Zynga is a data warehouse masked as a game company.

:::

In game development, AI is not just about decision-making and NPC behaviors. Today, we will focuse metrics, analytics, A/B testing, and rules testing tools, which are essential for making data-driven decisions in game AI.

## 1. Metrics & Analytics

::: tip "Reasoning"

Metrics and analytics are essential for understanding player behavior, engagement, and monetization. They help us refine mechanics, improve engagement, and optimize monetization.

::: 

### Why do we track metrics?

Game AI needs to be measurable to refine mechanics, improve engagement, and optimize monetization. Metrics help us understand:

- **Player behavior** (movement heatmaps, session length)
- **Difficulty balancing** (win/loss rates, completion rates)
- **Engagement & retention** (daily/weekly active users, churn rate)
- **Monetization impact** (in-game purchases, ad impressions)

### Key Tools for Analytics

- **GameAnalytics** – Free, widely used for tracking gameplay data.
- **Firebase Analytics** – Google’s tool for mobile & live games.
- **Unity Analytics** – Deep integration with Unity projects.
- **AWS Game Analytics** – Scalable analytics with machine learning insights.
- **PlayFab (Microsoft)** – End-to-end live game operations, including telemetry.

::: example "Example"

A multiplayer shooter game might use analytics to track hotspots where players die most often, helping developers rebalance map layouts.

:::

## 2. A/B Testing

::: tip "Definition"

A/B testing or split testing is a method of comparing two versions of a webpage or app against each other to determine which one performs better. It is a powerful tool for optimizing game mechanics, monetization, and engagement.

:::

A/B testing (or split testing) is a controlled experiment where players are divided into groups experiencing different versions of a feature.

### A/B testing process

1. **Segment your audience** – Divide players into two (or more) groups.
2. **Introduce controlled variations** – Example: One group gets harder enemies while another gets easier enemies.
3. **Measure key metrics** – Engagement, session time, or purchase rate.
4. **Statistically analyze the results** – Use hypothesis testing (e.g., t-tests, chi-square tests).
5. **Iterate based on results** – Implement the winning variation and test new ideas.
6. **Expose more players** – Gradually roll out the winning variation to more players.
7. **Monitor long-term impact** – Ensure the change has a lasting positive effect.

### What Can We A/B Test in AI?

- **Enemy AI difficulty** (Dynamic vs. Static AI difficulty scaling)
- **In-game economy** (Pricing models for virtual goods)
- **Tutorial effectiveness** (Different onboarding experiences)
- **AI-driven recommendations** (Which quests or rewards players prefer)

### Tools for A/B Testing:

- **Firebase Remote Config** (for live tuning game parameters)
- **Optimizely** (for testing UX and AI tweaks)
- **PlayFab Experiments** (Microsoft’s built-in A/B testing system)

::: example "Example"

A mobile puzzle game might A/B test different AI hints to see which ones help players complete levels faster.

:::

## 3. AI-Driven Predictive Analytics

Beyond basic metrics, we can use machine learning to predict player actions.

Techniques include:

- **Churn prediction** – Identifying players likely to quit.
- **Dynamic difficulty adjustment (DDA)** Adapting game AI difficulty in real-time.
- **Reinforcement Learning for NPCs** – AI improving itself based on player interactions.

Tools for AI-driven analytics:

- **TensorFlow + Unity ML-Agents** (AI training for NPC behaviors)
- **Amazon SageMaker** (Predictive models for player behavior)
- **DeepMind’s AlphaStar** (High-level AI game analysis)

## 4. Machinations: AI-Driven Game Balancing

[Machinations](https://machinations.io) is a visual modeling tool that simulates and analyzes game systems, economies, and AI-driven mechanics before implementation.

### Use Cases:

- **Balancing in-game economies** (Ensuring progression without pay-to-win issues)
- **Simulating AI behaviors** (How different difficulty modes affect engagement)
- **Testing player motivation loops** (Reward schedules, skill progression)

### In Class activity

We are going to use as an example a campaign for Willy Wonka’s Chocolate Factory. Will you be able to visit Wonka’s factory with a limited amount of money and time?

1. Access [Willy Wonka at Machinations](https://my.machinations.io/d/willy-wonka-factory-trip/f1c2402d6b5711efa81906fdf218a24f) 
2. Play with the sliders and see how the game changes.
3. Answer the following questions:
   - What is the goal of the game?
   - What are the resources available?
   - What are the actions that the player can take?
   - What are the rewards?
   - What are the penalties?

## Conclusion

Incorporating metrics, analytics, A/B testing, and machinations allows developers to optimize AI and enhance player experience. Whether using GameAnalytics for behavioral insights, A/B testing difficulty settings with Firebase, or simulating AI economies in Machinations.io, these tools help refine and perfect game AI.

Discussion Questions:
What AI-driven metrics would you track in a strategy game?
How can A/B testing be used to improve AI behaviors dynamically?
How might reinforcement learning improve AI analytics in a live game environment?
