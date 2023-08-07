import createStyles from '@mui/styles/createStyles';
import makeStyles from '@mui/styles/makeStyles';

const useStyles = makeStyles(() =>
  createStyles({
    root: {},
    btn: {
      'border': '2px solid #E8E9F4',
      'margin': '10px',
      'width': '60%',
      '@media (max-width: 700px)': {
        width: '100%',
        margin: '0px',
        marginBottom: '10px'
      }
    },
    btn_select: {
      'border': '2px solid #3AD29F',
      'margin': '10px',
      'width': '60%',
      '@media (max-width: 700px)': {
        width: '100%',
        margin: '0px',
        marginBottom: '10px'
      }
    }
  })
);

export default useStyles;
