let fs = require('fs');
let path = require('path');

// this function serves the purpose of filtering the routes generated with private tag in the app directory
// feel free to try to solve this smarter, I am just tired of trying

// Function to recursively find all "page.tsx" files
function findPageFiles(dir, basePath = '') {
  const files = fs.readdirSync(dir);
  let pagePaths = {};

  for (const file of files) {
    const filePath = path.join(dir, file);
    const stat = fs.statSync(filePath);

    if (stat.isDirectory()) {
      const nestedPaths = findPageFiles(filePath, path.join(basePath, file));
      pagePaths = { ...pagePaths, ...nestedPaths };
    } else if (file === 'page.tsx') {
      // create a key value for the path mapping. the key is the normalized urlPath without the dynamic and optional segments, remove "[locale]/" and "(string)" from the fullpath. The value is the full path to the page file.
      let urlPath = basePath
        .replace(/\[locale\]\//g, '') // Remove segments like [locale]
        .replace(/\[.*?\]/g, '') // Remove dynamic segments like [string]
        .replace(/\(.*?\)/g, '') // Remove segments like (string)
        .replace(/\/+/g, '/') // Replace multiple slashes with a single slash
        .replace(/\/$/, ''); // Remove trailing slash
      // if the urlPath is empty, set it to "/"
      urlPath = urlPath === '' ? '/' : `${urlPath}`;

      pagePaths[`${urlPath}`] = filePath.replace(/^.*?\/app/, '');
    }
  }

  return pagePaths;
}

function generatePathMapping() {
  // Define the directory containing the pages
  const pagesDir = path.join(__dirname, 'app');

  // Generate paths
  const pathMapping = findPageFiles(pagesDir);

  // Format mapping to a TypeScript export
  const mappingContent = `export const pathMapping: Record<string, string> = ${JSON.stringify(pathMapping, null, 2)};`;

  // Write to a new TypeScript file
  fs.writeFileSync(path.join(__dirname, 'pathMapping.ts'), mappingContent);
  console.log('Path mapping generated successfully!');
}

module.exports = generatePathMapping; // Export the function for use in next.config.js
