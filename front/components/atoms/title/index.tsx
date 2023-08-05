import Typography from '@mui/material/Typography';
import React from 'react';

interface IProps {
  title: string;
  center?: boolean;
  variant: 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6';
}

const Title = ({ title, center, variant }: IProps) => {

  return (
    <div>
      <Typography title={title} variant={variant} align={center ? 'center' : 'left'}>
        {title}
      </Typography>
    </div>
  );
  
};

export default Title;
