// require fs and path
const fs = require("fs");
const { join } = require("path");

// get the parameters
const args = process.argv.slice(2);
console.log(args);

// ensure both parameters are provided
if (args.length !== 2) {
  console.error("Please provide two directories");
  process.exit(1);
}

const dir1 = args[0];
const dir2 = args[1];

// ensure both directories exist
if (!fs.existsSync(dir1) || !fs.existsSync(dir2)) {
  console.error("One of the directories does not exist");
  process.exit(1);
}

// function to create a list of all files in dir, including subdirectories
function getAllFiles(dir) {
  const files = [];
  const list = fs.readdirSync(dir, { withFileTypes: true });
  for (const file of list) {
    const path = join(dir, file.name);
    if (file.isDirectory()) {
      files.push(...getAllFiles(path));
    } else {
      files.push(path);
    }
  }
  return files;
}

// get all files in both directories
let files1 = getAllFiles(dir1);
let files2 = getAllFiles(dir2);

// remove root directory from both lists
let filesFiltered1 = [];
let filesFiltered2 = [];
files1.forEach(file => {
  filesFiltered1.push(file.replace(dir1, ""));
});
files2.forEach(file => {
  filesFiltered2.push(file.replace(dir2, ""));
});

files1 = filesFiltered1;
files2 = filesFiltered2;

// compare the two lists
const onlyIn1 = files1.filter(f => !files2.includes(f));
const onlyIn2 = files2.filter(f => !files1.includes(f));
const common = files1.filter(f => files2.includes(f));

// function to get the dirname
function dirname(path) {
  return path.split("/").slice(0, -1).join("/");
}

// copy onlyIn1 to dir2, and ensure that the destination directories exist
onlyIn1.forEach(file => {
  const dest = join(dir2, file);
  if (!fs.existsSync(dirname(dest))) {
    fs.mkdirSync(dirname(dest), { recursive: true });
  }
  fs.copyFileSync(join(dir1, file), dest);
});

// copy onlyIn2 to dir1, and ensure that the destination directories exist
onlyIn2.forEach(file => {
  const dest = join(dir1, file);
  if (!fs.existsSync(dirname(dest))) {
    fs.mkdirSync(dirname(dest), { recursive: true });
  }
  fs.copyFileSync(join(dir2, file), dest);
});

// update common files in both directories. If one side is more recent, copy it to the other
common.forEach(file => {
  const src1 = join(dir1, file);
  const src2 = join(dir2, file);
  const src1stat = fs.statSync(src1);
  const src2stat = fs.statSync(src2);
  if (src1stat.mtimeMs > src2stat.mtimeMs) {
    fs.copyFileSync(src1, src2);
  } else if (src2stat.mtimeMs > src1stat.mtimeMs) {
    fs.copyFileSync(src2, src1);
  }
});