import { Api } from '@game-guild/apiclient';
import CourseEntity = Api.CourseEntity;
import UserEntity = Api.UserEntity;

export const COURSES: CourseEntity[] = [
  {
    id: '1',
    slug: 'python',
    owner: {
      username: 'tolstenko',
    } as UserEntity,
    title: 'Python to Programming through Python',
    summary:
      'Students will learn the history and basics of computing as well as the fundamentals of Python programming. General topics include: the history of computing, number systems, Boolean logic, algorithm design and implementation, and modern computer organization. Programming topics include: memory and variables, data types, mathematical operations, basic file I/O, decision-making, repetitions, functions, and list basics.',
    thumbnail: {
      path: 'https://upload.wikimedia.org/wikipedia/commons/thumb/c/c3/Python-logo-notext.svg',
      filename: 'Python-logo-notext.svg',
    } as Api.ImageEntity,
  } as CourseEntity,
  // {
  //   id: '2',
  //   slug: 'vue',
  //   name: 'Vue.js',
  //   description: 'The Progressive JavaScript Framework',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/5/53/Vue.js_Logo.svg/320px-Vue.js_Logo.svg.png',
  // },
  // {
  //   id: '3',
  //   slug: 'angular',
  //   name: 'Angular',
  //   description: 'One framework. Mobile & desktop.',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/c/cf/Angular_full_color_logo.svg/320px-Angular_full_color_logo.svg.png',
  // },
  // {
  //   id: '4',
  //   slug: 'svelte',
  //   name: 'Svelte',
  //   description: 'Cybernetically enhanced web apps',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/1/1b/Svelte_Logo.svg/320px-Svelte_Logo.svg.png',
  // },
  // {
  //   id: '5',
  //   slug: 'ember',
  //   name: 'Ember.js',
  //   description: 'A framework for ambitious web developers',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/2/27/Ember.js_Logo_and_Mascot.svg/320px-Ember.js_Logo_and_Mascot.svg.png',
  // },
  // {
  //   id: '6',
  //   slug: 'backbone',
  //   name: 'Backbone.js',
  //   description: 'Give your JS App some Backbone with Models, Views, Collections, and Events',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/6/6c/Backbone.js_Logo.svg/320px-Backbone.js_Logo.svg.png',
  // },
  // {
  //   id: '7',
  //   slug: 'meteor',
  //   name: 'Meteor',
  //   description: 'An ultra-simple, database-everywhere, data-on-the-wire, pure-Javascript web framework',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/9/93/MeteorJS_Logo.png/320px-MeteorJS_Logo.png',
  // },
  // {
  //   id: '8',
  //   slug: 'polymer',
  //   name: 'Polymer',
  //   description: 'Build modern apps using web components',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/8/8e/Polymer_Project_logo.png/320px-Polymer_Project_logo.png',
  // },
  // {
  //   id: '9',
  //   slug: 'aurelia',
  //   name: 'Aurelia',
  //   description: 'A JavaScript client framework for mobile, desktop and web leveraging simple conventions and empowering creativity',
  //   thumbnailUrl: 'https://upload.wikimedia.org/wikipedia/commons/thumb/3/3b/Aurelia-logo.png/320px-Aurelia-logo.png',
  // },
];
