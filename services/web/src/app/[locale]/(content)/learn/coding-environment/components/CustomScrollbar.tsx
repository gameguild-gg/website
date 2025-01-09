import React, { ReactNode } from 'react';
import { Scrollbars } from 'react-custom-scrollbars-2';

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

  return (
    <Scrollbars
      className={className}
      renderTrackVertical={({ style, ...props }) => (
        <div {...props} style={{ ...style, ...getTrackStyle(), right: 2, bottom: 2, top: 2, width: 8 }} />
      )}
      renderThumbVertical={({ style, ...props }) => (
        <div {...props} style={{ ...style, ...getThumbStyle(), borderRadius: 4 }} />
      )}
      renderView={props => (
        <div {...props} style={{ ...props.style, overflowX: 'hidden' }} />
      )}
    >
      {children}
    </Scrollbars>
  );
};

export default CustomScrollbar;

