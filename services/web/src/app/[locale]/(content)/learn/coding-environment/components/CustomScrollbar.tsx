import React, { ReactNode, useRef, useEffect } from 'react';
import { Scrollbars } from 'react-custom-scrollbars-2';

interface CustomScrollbarProps {
  children: ReactNode;
  className?: string;
  mode: 'light' | 'dark' | 'high-contrast';
}

const CustomScrollbar: React.FC<CustomScrollbarProps> = ({ children, className, mode }) => {
  const contentRef = useRef<HTMLDivElement>(null);

  const getTrackStyle = () => {
    switch (mode) {
      case 'light':
        return { backgroundColor: '#f1f1f1' };
      case 'dark':
        return { backgroundColor: '#2a2a2a' };
      case 'high-contrast':
        return { backgroundColor: '#000000' };
      default:
        return { backgroundColor: '#f1f1f1' }; // Default to light mode
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
      default:
        return { backgroundColor: '#888888' }; // Default to light mode
    }
  };

  useEffect(() => {
    // Log the contentRef to check if it's correctly referencing the child element
    console.log("contentRef:", contentRef);
  }, []);

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
        <div {...props} ref={contentRef} style={{ ...props.style, overflowX: 'hidden' }} />
      )}
    >
      {children}
    </Scrollbars>
  );
};

export default CustomScrollbar;

