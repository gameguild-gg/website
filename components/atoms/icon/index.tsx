import Icon from '@mui/material/Icon';
import React from 'react';

interface IProps {
  icon: string;
  size: number;
}

const Symbol = ({ icon, size }: IProps) => {

  return (
    <div>
		<Icon sx={{ fontSize: size + 'px' }} >{icon}</Icon>;
    </div>
  );

};

export default Symbol;