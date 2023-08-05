import makeStyles from '@mui/styles/makeStyles';
import Typography from '@mui/material/Typography';
import { useMemo } from 'react';
import { colors } from '../../../theme';
import React from 'react';

interface IProps {
  text: string;
  center?: boolean;
  isExpired?: boolean;
  strong?: boolean;
  small?: boolean;
  className?: string;
}

const useStyles = makeStyles(() => ({
  strong: {
    fontWeight: 600
  },
  text: {
    fontWeight: 500
  },
  expiredColor: {
    color: colors.expired
  }
}));

const Text = ({
  text,
  isExpired = false,
  strong = false,
  small = false,
  className = ''
}: IProps) => {
  const classes = useStyles();

  const getClass = useMemo(() => {
    if (strong) {
      return classes.strong;
    }

    if (isExpired) {
      return classes.expiredColor;
    }

    if (className) {
      return className;
    }

    return classes.text;
  }, [className, classes, strong, isExpired]);

  return (
    <Typography variant={small ? 'body2' : 'body1'} className={getClass}>
      {text}
    </Typography>
  );
};
export default Text;