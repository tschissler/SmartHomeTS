// Minimal service worker — enables PWA installability.
// No offline caching since SmartHome requires a live server connection.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.clients.claim());
