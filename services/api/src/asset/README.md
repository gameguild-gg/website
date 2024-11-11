# Asset management system

## Features

- Cache assets in file system in a folder splitting by hash every two characters. If the hash is AABBCCDDEEFF it will be stored in `AA/BB/AABBCCDDEEFF.jpg`
- Optimize images on the fly and cache them locally in a folder
- Add suffixes to the file name if resized or optimized. For example, `AABBCCDDEEFF_320x480_75.jpg`
- Set http caching flags for cloudflare to effectively cache assets and avoid unnecessary requests
- Manage different sources such as s3, local, etc
- Delete old assets from cache. Every XXX seconds, check if the total size of the folder cache is bigger than YYY MB, add all files to a priority queue ordered by last touched date and delete the oldest files until the total size is less than half of the threshold.  
- Every request to an asset will update the last touched date on the file system
- Reference counter. If the amount of references to an asset is 0, it will be deleted from data base and the source system.
- Asset processing queue. If an asset is not found in the cache, it will be added to the queue to be processed and cached. The request should wait until the asset is processed and cached. It should use a server queueing system to avoid triggering multiple optimizations for the same asset at the same time. 

## Env vars

- `ASSET_CACHE_DIR` - Directory to cache assets
- `ASSET_CACHE_TTL` - Cache TTL in seconds
- `ASSET_CACHE_SIZE` - Cache size in MB, if it is bigger than this, delete old assets
- `ASSET_SOURCE_XXXX_TYPE` - Source type (s3, IPFS, Cloudinary...)
- `ASSET_SOURCE_XXXX_ENDPOINT` - Source server API domain, IP or URL
- `ASSET_SOURCE_XXXX_PORT` - Source server API port
- `ASSET_SOURCE_XXXX_KEY` - Source key, email or username if applicable
- `ASSET_SOURCE_XXXX_SECRET` - Source password or secret if applicable
- `ASSET_SOURCE_XXXX_BUCKET` - Source bucket or path if applicable