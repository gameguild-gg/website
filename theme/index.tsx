import { green, red, yellow } from '@mui/material/colors';
import { createTheme } from '@mui/material/styles';

export const colors = {
  error: red.A400,
  primaryLight: '#202430',
  primaryDark: '#F8F8FD',
  secondaryDark: '#7C8493',
  secondaryLight: '#515B6F',
  backgroundColor: 'white',
  icon: '#FFFFFF',
  info: '#D6DDEB',
  buttonDefault: '#31E080',
  borderColor: '#D6DDEB',
  expired: '#ababab',
  green: '#3AD29F',
  success: green.A700,
  successLight: green.A400,
  warning: yellow[800]
};

export const propsOverride = {};

export const darkTheme = createTheme({
  typography: {
    fontFamily: ['Lato'].join(',')
  },

  palette: {
    primary: {
      main: colors.green
    },
    secondary: {
      main: colors.primaryDark
    },
    text: {
      primary: colors.primaryLight
    },
    error: {
      main: colors.error
    },
    background: {
      default: colors.backgroundColor
    },
    action: {
      disabled: colors.borderColor,
      disabledBackground: colors.secondaryDark
    }
  }
});

export const lightTheme = createTheme({
  palette: {
    primary: {
      main: '#000'
    },
    secondary: {
      main: '#ccc'
    },
    error: {
      main: red.A400
    },
    background: {
      default: '#fff'
    }
  },
  spacing: 16
});
