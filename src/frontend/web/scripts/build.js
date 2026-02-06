process.env.NODE_ENV = process.env.NODE_ENV || 'production';
process.env.NEXT_PUBLIC_API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:3000';

await import('../src/shared/config.js');

console.log('Build complete.');
