self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'smartfield-time-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
  /\.dll$/,
  /\.pdb$/,
  /\.wasm$/,
  /\.html$/,
  /\.js$/,
  /\.json$/,
  /\.css$/,
  /\.woff$/,
  /\.png$/,
  /\.svg$/
];
const offlineAssetsExclude = [/^service-worker\.js$/];

self.addEventListener('install', event => {
  self.skipWaiting();
  event.waitUntil(onInstall());
});

self.addEventListener('activate', event => {
  event.waitUntil(onActivate());
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') {
    return;
  }

  event.respondWith(onFetch(event));
});

async function onInstall() {
  const assetsRequests = self.assetsManifest.assets
    .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
    .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
    .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

  const cache = await caches.open(cacheName);
  await cache.addAll(assetsRequests);
}

async function onActivate() {
  const cacheKeys = await caches.keys();
  await Promise.all(cacheKeys
    .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
    .map(key => caches.delete(key)));

  await self.clients.claim();
}

async function onFetch(event) {
  const cachedResponse = await caches.match(event.request);
  return cachedResponse ?? fetch(event.request);
}
