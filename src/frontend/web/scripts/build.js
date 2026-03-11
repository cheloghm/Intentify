process.env.NODE_ENV = process.env.NODE_ENV || 'production';
await import('../src/shared/config.js');

console.log('Build complete.');
