'use client';

function Footer() {
  return (
    <div className="text-white text-center bg-[#2a2a2a]">
      <div className="flex justify-center w-full align-middle">
        <div className="justify-between max-w-[1440px] w-full align-center items-center content-center">
          <div className="flex">
            <a href="#">
              <img
                src="/assets/images/whatsapp-icon.svg"
                className="w-[30px] h-[30px] m-3"
              />
            </a>
            <a href="#">
              <img
                src="/assets/images/discord-icon.svg"
                className="w-[30px] h-[30px] m-3"
              />
            </a>
          </div>
          <div>Game Guild © 2024 All Rights Reserved</div>
          <div>
            <a href="#">Privacy Policy</a>
            {' | '}
            <a href="#">Terms of Service</a>
          </div>
        </div>
      </div>
    </div>
  );
}

export default Footer;
