import React from 'react';

type Props = React.HTMLAttributes<HTMLDivElement> & {
  children?: React.ReactNode;
};

function PageFooter({ children }: Readonly<Props>) {
  return (
    <div className="text-center text-white bg-[#2a2a2a] overflow-hidden">
      <div className="w-full justify-center align-center">
        <div
          style={{
            maxWidth: '1440px',
            width: '100%',
            display: 'inline-flex',
            alignItems: 'center',
            alignContent: 'center',
          }}
          className="grid grid-cols-3 justify-between max-w-[1440px] w-full flex items-center"
        >
          <div className="flex">
            <a href="#">
              <img
                style={{ width: 30, height: 30, margin: 3 }}
                src="assets/images/whatsapp-icon.svg"
              />
            </a>
            <a href="#">
              <img
                style={{ width: 30, height: 30, margin: 3 }}
                src="assets/images/discord-icon.svg"
              />
            </a>
          </div>
          <div>Game Guild © 2024 All Rights Reserved</div>
          <div>
            <a href="#" className="text-white">
              Privacy Policy
            </a>{' '}
            |{' '}
            <a href="#" className="text-white">
              Terms of Service
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}

export { PageFooter };
