import { Api } from '@game-guild/apiclient';
import ChapterEntity = Api.ChapterEntity;
import CourseEntity = Api.CourseEntity;
import ImageEntity = Api.ImageEntity;
import UserEntity = Api.UserEntity;
import LectureEntity = Api.LectureEntity;

import syllabusBody from './syllabus.md';

/*
Week 1: 1/13 - 1/17
Week 2*: 1/20 - 1/24
Week 3: 1/27 - 1/31
Week 4: 2/3 - 2/7
Week 5: 2/10 - 2/14
Week 6: 2/17 - 2/21
Week 7: 2/24 - 2/28
Week 8: 3/3 - 3/7
Week 9*: Spring Break 3/10 - 3/14
Week 10: 3/17 - 3/21
Week 11: 3/24 - 3/28
Week 12: 3/31 - 4/4
Week 13*: 4/7 - 4/11
Week 14: 4/14 - 4/18
Week 15: 4/21 - 4/25
Week 16: Final Exams 4/28 - 5/2
Semester begins Monday 1/13 | No Classes MLK Day: Monday, 1/20 | Spring Break 3/10-14  |  No Classes: Friday 4/11
*/

const syllabus: LectureEntity = {} as LectureEntity;

syllabus.body = syllabusBody;

export default syllabus;
