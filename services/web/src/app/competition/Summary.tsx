import React from "react";
import { Typography } from 'antd';

const Summary: React.FC = () => {
   return (
      <>
        <Typography.Title>Summary</Typography.Title>
        <Typography.Paragraph>
          This application is an AI chess competition platform. Initially developed for the course "Advanced AI for Games" at Champlain College. The platform allows users to submit their chess bots and compete against each other. The platform also provides a leaderboard to track the performance of the bots.
        </Typography.Paragraph>
        <Typography.Title level={2}>How to Submit</Typography.Title>
        <Typography.Paragraph>
          To submit a bot, you need to go to the "Submit section" page and upload a zip file containing the source code of your bot. The zip file must contain the source code of your bot in C++ and/or C++ header files. The zip file must not contain any subfolders. The zip file must be less than 10MB. The zip file must contain at least one .cpp or .h file.
        </Typography.Paragraph>
        <Typography.Title level={2}>Bot implementation</Typography.Title>
        <Typography.Paragraph>
          The bot implement a function called "Move" on namespace "ChessSimulator" that takes a string representing the current state of the board and returns a string representing the move to make. The bot must be able to play as white and black.
        </Typography.Paragraph>
        <Typography.Paragraph>
          You might want to <Typography.Link href="https://github.com/InfiniBrains/chess-competition" target="_blank"> check the repo </Typography.Link> for more information. You will find debugger, memory leak detection, and performance tests.
        </Typography.Paragraph>
      </>
    )
}

export default Summary