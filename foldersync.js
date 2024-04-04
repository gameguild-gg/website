// require fs and path
const fs = require("fs");
const path = require("path");

// get the parameters
const args = process.argv.slice(2);

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
    const filePath = path.join(dir, file.name);
    if (file.isDirectory()) {
      files.push(...getAllFiles(filePath));
    } else {
      files.push(filePath);
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
  filesFiltered1.push(path.relative(dir1, file));
});
files2.forEach(file => {
  filesFiltered2.push(path.relative(dir2, file));
});

files1 = filesFiltered1;
files2 = filesFiltered2;

// compare the two lists
const onlyIn1 = files1.filter(f => !files2.includes(f));
const onlyIn2 = files2.filter(f => !files1.includes(f));
const common = files1.filter(f => files2.includes(f));

function mkdirSyncRecursive(dir) {
  const parts = dir.split(path.sep);
  for (let i = 1; i <= parts.length; i++) {
    const subPath = path.join(...parts.slice(0, i));
    if (!fs.existsSync(subPath)) {
      fs.mkdirSync(subPath);
    }
  }
}

// copy onlyIn1 to dir2, and ensure that the destination directories exist
onlyIn1.forEach(file => {
  const dest = path.join(dir2, file);
  mkdirSyncRecursive(path.dirname(dest));
  fs.copyFileSync(path.join(dir1, file), dest);
});

// copy onlyIn2 to dir1, and ensure that the destination directories exist
onlyIn2.forEach(file => {
  const dest = path.join(dir1, file);
  mkdirSyncRecursive(path.dirname(dest));
  fs.copyFileSync(path.join(dir2, file), dest);
});

async function touchFile(filePath, time = new Date()) {
  await fs.promises.utimes(filePath, time, time);
}

// update common files in both directories. If one side is more recent, copy it to the other
common.forEach(file => {
  const time = new Date();
  const src1 = path.join(dir1, file);
  const src2 = path.join(dir2, file);
  const src1Stat = fs.statSync(src1);
  const src2Stat = fs.statSync(src2);
  if (src1Stat.mtimeMs > src2Stat.mtimeMs) {
    fs.copyFileSync(src1, src2);
    touchFile(src1, time);
    touchFile(src2, time);
  } else if (src2Stat.mtimeMs > src1Stat.mtimeMs) {
    fs.copyFileSync(src2, src1);
    touchFile(src1, time);
    touchFile(src2, time);
  }
});
