import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/chess/ui/card';
import { ExternalLink } from 'lucide-react';

export default function SummaryContent() {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>About the Platform</CardTitle>
          <CardDescription>AI Chess Competition Platform</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p>
            This application is an AI chess competition platform. Initially developed for the course Advanced AI for Games at Champlain College. The platform
            allows users to submit their chess bots and compete against each other. The platform also provides a leaderboard to track the performance of the
            bots.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>How to Submit</CardTitle>
          <CardDescription>Guidelines for submitting your chess bot</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p>
            To submit a bot, you need to go to the Submit section page and upload a zip file containing the source code of your bot. The zip file must contain
            the source code of your bot in C++ and/or C++ header files. The zip file must not contain any subfolders. The zip file must be less than 10MB. The
            zip file must contain at least one .cpp or .h file.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Bot Implementation</CardTitle>
          <CardDescription>Technical requirements for your chess bot</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p>
            The bot must implement a function called Move on namespace ChessSimulator that takes a string representing the current state of the board and
            returns a string representing the move to make. The bot must be able to play as white and black.
          </p>
          <div className="flex items-center space-x-2 mt-4">
            <p>
              You might want to check the
              <a
                href="https://github.com/InfiniBrains/chess-competition"
                target="_blank"
                rel="noopener noreferrer"
                className="text-primary hover:underline inline-flex items-center mx-1"
              >
                repository
                <ExternalLink className="h-4 w-4 ml-1" />
              </a>
              for more information. You will find debugger, memory leak detection, and performance tests.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
