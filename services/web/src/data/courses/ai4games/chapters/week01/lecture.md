# Random and Noise

<details>
<summary>Teacher notes</summary>

- **Day 1**: Teacher Introduction; Course Overview; Expectations; FERPA Waiver consent form for using github; Form for receiving feedback about their expectations and topics; 
- **Day 2**: Review expectations in class; Introduction to Randomness and Noise; Invite to explore fastnoise2 repo and share their generations with noise transformations;

</details>

## Randomness in Games

Randomness gives diversity and unpredictability to games, which in many times might increase the fun and replayability of a game. However, randomness can also be a double-edged sword, as it can lead to unfairness, frustration, and lack of control for players. So we need to fine tune and control randomness in a way it can enhance the gameplay experience. For more information about the psychology behind it, check out this [Octalysis article](https://octalysisgroup.com/2015/06/unpredictability-curiosity-yang-and-ying-of-gamification/).

### Pseudo Random Number Generation (PRNG)

::: note
There is no true randomness in computers, as they are deterministic machines. Computers use pseudo-random number generators (PRNGs) to simulate randomness. PRNGs generate sequences of numbers that appear random but are actually deterministic. The quality of a PRNG is measured by its period (how long before it repeats), distribution (how evenly the numbers are spread), and predictability (how difficult it is to guess the next number).
:::

There are a bunch of algorithms to generate pseudo-random numbers, and some of the most common are:

- Mersenne Twister
- Linear Congruential Generator (LCG)
- XOR Shift

But there are many others, and the choice of which one to use depends on the specific requirements of the application. Check a extensive list of PRNGs [here](https://en.wikipedia.org/wiki/List_of_random_number_generators). 

### Measuring Randomness

Randomness can be measured in different ways, and some of the most common metrics are:

- **Quality**: How well the PRNG passes statistical tests for randomness.
- **Period**: How long before the sequence repeats.
- **Distribution**: How evenly the numbers are spread.
- **Predictability**: How difficult it is to guess the next number.
- **Seed**: The initial value used to start the PRNG sequence.


### Types of Noise

![Noises](https://user-images.githubusercontent.com/6199226/202872480-512ab0ef-7210-4eff-8c3c-8179701e1f1e.jpg)
[Source](https://github.com/covexp/cuda-noise)

1. Value Noise
    - A grid filled with random values, which are interpolated to create smooth gradients.
    - Uses: Basic procedural texture generation, less common due to Perlin/Simplex improvements.
2. Perlin Noise
    - Characteristics: Smooth gradient noise; produces continuous, organic patterns.
    - Uses: Terrain generation, cloud formation, and procedural textures.
    - [wiki](https://en.wikipedia.org/wiki/Perlin_noise)
3. Simplex Noise
    - Characteristics: Improved version of Perlin noise; computationally cheaper and less grid-aligned artifacts.
   - Uses: Terrain generation, water simulations, and texture synthesis.
4. Gaussian Noise
    - Characteristics: Noise distributed in a bell-curve (Gaussian) distribution.
    - Uses: Image post-processing, procedural randomness with realistic distribution.
5. Wavelet Noise
    - Characteristics: Alternative to Perlin noise with higher performance in some cases.
    - Uses: Advanced procedural generation techniques.
6. Cellular Noise
    - Characteristics: Grid-like patterns, related to Voronoi and Worley.
    - Uses: Procedural maps and natural patterns.
7. Voronoi Noise
    - Characteristics: Creates cell-like patterns based on distance to nearest points.
    - Uses: Procedural textures like cracked surfaces, cell structures, and organic patterns.
    - ![Voronoise](https://iquilezles.org/articles/voronoise/gfx01.jpg)
    - [ShdarerToy](https://www.shadertoy.com/embed/Xd23Dh)
8. Worley Noise
    - Characteristics: Distance-based noise, often similar to Voronoi, emphasizing distance metrics.
    - Uses: Organic textures (e.g., stone, skin), and effects like lava or veins.
    - [wiki](https://en.wikipedia.org/wiki/Worley_noise)
9. Ridged Noise
    - Characteristics: Inverts and enhances peaks of Perlin or Simplex noise.
    - Uses: Mountain range generation and sharp geological structures.
    - [Explanation](https://iquilezles.org/articles/morenoise/)
    - [video](https://www.youtube.com/watch?v=gsJHzBTPG0Y)

### Transformations 

1. Fractal Noise
    - Characteristics: Layers (octaves) of noise combined to create more complex, hierarchical patterns.
    - Uses: Realistic terrain, clouds, and texture details.
2. Domain Warping
    - Characteristics: Noise modified by another noise to create distorted, chaotic effects.
    - Uses: Alien terrain, surreal textures, and unpredictable natural patterns. 
3. FBM (Fractional Brownian Motion)
    - Characteristics: Combines multiple noise layers with different frequencies and amplitudes.
    - Uses: Clouds, terrains, and water. 
4. Turbulence Noise
    - Characteristics: Absolute value of noise combined for "chaotic" patterns.
    - Uses: Fire, smoke, and abstract textures.

### In class Activity

1. Go to [fastnoise2 repo](https://github.com/Auburn/FastNoise2)
2. Go to the releases section
3. Click assets and download the latest build version for your platform
4. Unzip the file
5. Go to the bin folder
6. Run the executable
7. Go to the [repo home](https://github.com/Auburn/FastNoise2) and try to implement the same scenario with the same blocks
8. Share your results with the class

Some generated content by the students on class can be found [here](https://imgur.com/gallery/procedural-content-generation-with-noise-eT3hhyL)

![a](https://i.imgur.com/oIVoWs5.png)
![b](https://i.imgur.com/7n09Awy.png)
![c](https://i.imgur.com/KVQEcME.png)
![d](https://i.imgur.com/PgEKKOn.png)
![e](https://i.imgur.com/J9iqPi5.png)
