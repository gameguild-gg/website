// import { Test } from "@nestjs/testing";
// import { ContentService } from "../../src/cms/content.service";
// import { ContentController } from "../../src/cms/content.controller";
// import { CourseEntity } from "src/cms/entities/course.entity";
// import { getRepositoryToken } from "@nestjs/typeorm";
// import { Repository } from "typeorm";

// describe("ContentController", () => {
//     let controller: ContentController;
//     let service: ContentService;
//     let courseRepository: Repository<CourseEntity>;

//     const user = {};

//     beforeEach(async () => {
//         const moduleRef = await Test.createTestingModule({
//             controllers: [ContentController],
//             providers: [
//                 ContentService,
//                 { 
//                     provide: getRepositoryToken(CourseEntity),
//                     useValue: courseRepository,
//                 }
//             ],
//           }).compile();

//           service = moduleRef.get<ContentService>(
//             // @InjectRepository(CourseEntity)
//             // private readonly courseRepository: Repository<CourseEntity>,
//             // @InjectRepository(LectureEntity)
//             // private readonly lectureRepository: Repository<LectureEntity>,
//             // @InjectRepository(ChapterEntity)
//             // private readonly chapterRepository: Repository<ChapterEntity>,
//             // @InjectRepository(PostEntity)
//             // private readonly postRepository: Repository<PostEntity>,
//             // private readonly userService: UserService,
//           );
//         controller = moduleRef.get<ContentController>(ContentService);
//     });

//     describe('getAll', () => {
//         it("Should return an array of created courses.", async() => {
//             const courses: CourseEntity[] = [];
//             jest.spyOn(service, 'getAllCourses').mockImplementation(async () => courses);

//             expect(await controller.getAllCourses()).toBe(courses);
//         });
//         // TODO
//     });

// });

