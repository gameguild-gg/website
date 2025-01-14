import React from 'react';

export default async function Page() {
  return (
    <div>
    <header>
        <h1>About GameGuild</h1>
    </header>
    <main>
        <section>
            <p>
                <strong>GameGuild</strong> is a platform dedicated to bringing together gamers, developers, and enthusiasts through a unique combination of educational resources, a blog, and exciting game tests.
            </p>
            <p>
                Our mission is to foster a thriving community where users can learn, share ideas, and collaborate on creating amazing gaming experiences.
            </p>
        </section>
        <section>
            <h2>What We Offer</h2>
            <ul>
                <li><strong>Educational Resources:</strong> Tutorials and guides to help you improve your skills in game development and design.</li>
                <li><strong>Blog:</strong> Stay updated with the latest trends, tips, and stories in the gaming world.</li>
                <li><strong>Game Testing:</strong> Try out new games and provide valuable feedback to developers.</li>
            </ul>
        </section>
        <section>
            <h2>Our Vision</h2>
            <p>
                We aim to be a hub for creativity and innovation in the gaming industry, empowering individuals to turn their ideas into reality.
            </p>
        </section>
    </main>
    <footer>
        <p>&copy; 2025 GameGuild. All rights reserved.</p>
    </footer>
    </div>
  );
}
