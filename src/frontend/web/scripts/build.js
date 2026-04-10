import fs   from 'fs';
import path  from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const version   = Date.now();

// Files to version-stamp (add ?v= to all local asset references)
const htmlFiles = [
  path.join(__dirname, '../public/index.html'),
  path.join(__dirname, '../public/login.html'),
  path.join(__dirname, '../public/register.html'),
  path.join(__dirname, '../public/admin-setup.html'),
];

htmlFiles.forEach(file => {
  if (!fs.existsSync(file)) return;
  let content = fs.readFileSync(file, 'utf8');
  // Replace bare .js/.css references (local paths only, not http/https) with versioned URL
  content = content.replace(
    /(src|href)="((?!https?:\/\/)[^"]+\.(js|css))(\?[^"]*)?"(\s|>|\/)/g,
    (_match, attr, url, _ext, _query, end) => `${attr}="${url}?v=${version}"${end}`
  );
  fs.writeFileSync(file, content, 'utf8');
  console.log(`Versioned: ${path.basename(file)} → ?v=${version}`);
});

// Update build version in env-config.js
const envConfig = path.join(__dirname, '../public/env-config.js');
if (fs.existsSync(envConfig)) {
  let config = fs.readFileSync(envConfig, 'utf8');
  config = config.replace(/\nwindow\.__BUILD_VERSION__\s*=\s*'[^']*';\n?/g, '');
  config += `\nwindow.__BUILD_VERSION__ = '${version}';\n`;
  fs.writeFileSync(envConfig, config, 'utf8');
  console.log('Updated build version in env-config.js');
}

console.log(`Build complete. Version: ${version}`);
