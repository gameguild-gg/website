import React, { ReactNode } from 'react';
import { Scrollbar } from 'react-scrollbars-custom';

interface CustomScrollbarProps {
  children: ReactNode;
  className?: string;
  mode: 'light' | 'dark' | 'high-contrast';
}

const CustomScrollbar: React.FC<CustomScrollbarProps> = ({ children, className, mode }) => {
  const getTrackStyle = () => {
    switch (mode) {
      case 'light':
        return { backgroundColor: '#f1f1f1' };
      case 'dark':
        return { backgroundColor: '#2a2a2a' };
      case 'high-contrast':
        return { backgroundColor: '#000000' };
    }
  };

  const getThumbStyle = () => {
    switch (mode) {
      case 'light':
        return { backgroundColor: '#888888' };
      case 'dark':
        return { backgroundColor: '#555555' };
      case 'high-contrast':
        return { backgroundColor: '#ffff00' };
    }
  };

  // return (
  //   <Scrollbar
  //     style={{ width: '100%', height: '100%' }}
  //     className={className}
  //     trackYProps={{
  //       style: getTrackStyle()
  //     }}
  //     thumbYProps={{
  //       style: getThumbStyle()
  //     }}
  //   >
  //     {children}
  //   </Scrollbar>
  // );
  return(<></>)
};

export default CustomScrollbar;

