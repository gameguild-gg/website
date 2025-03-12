// download python and clang wasmer, compress as brotli and place it at public/assets/wasmer folder

// async fs
const fs = require('fs');
const brotliPromise = require('brotli-wasm');

// fetch function to get the wasm file
async function fetchPackage(packageName) {
  console.log('Fetching wasm file description from registry');
  const url = `https://registry.wasmer.io/graphql`;
  const query = `{
    getPackage(name: "python/python") {
        versions {
          version
          v3: distribution(version: V3) {
            piritaDownloadUrl
          }
        }
    }
}`;

  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ query }),
  });
  const json = await response.json();
  let webcUrl = json.data.getPackage.versions[0].v3.piritaDownloadUrl;

  console.log('Webc download url:', webcUrl);
  const webcResponse = await fetch(webcUrl);

  // save the response
  const dest = `public/assets/wasmer/${packageName}.webc`;

  // ensure the directory exists
  const folder = dest.split('/').slice(0, -1).join('/');
  fs.mkdirSync(folder, { recursive: true });
  // write the content of the response to the file at dest
  const buffer = Buffer.from(await webcResponse.arrayBuffer());
  fs.writeFileSync(dest, buffer);

  // compress it
  const brotliPromise = require('brotli-wasm');
  const brotli = await brotliPromise;

  // todo make this work as stream to avoid consuming too much memory
  // load the file
  const compressed = brotli.compress(buffer);
  fs.writeFileSync(`public/assets/wasmer/${packageName}.webc.br`, compressed);
}

console.log('Fetching wasm files from registry and compressing them');

const packages = ['python/python', 'clang/clang'];
(async () => {
  for (const packageName of packages) {
    await fetchPackage(packageName);
  }
})();
