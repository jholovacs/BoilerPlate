/**
 * Generates version.json and components.json at build time.
 * Run before ng build.
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const rootDir = path.resolve(__dirname, '..');
const pkgPath = path.join(rootDir, 'package.json');
const lockPath = path.join(rootDir, 'package-lock.json');
const versionOut = path.join(rootDir, 'version.json');
const componentsOut = path.join(rootDir, 'components.json');

const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
const lock = fs.existsSync(lockPath)
  ? JSON.parse(fs.readFileSync(lockPath, 'utf8'))
  : { packages: {} };

const buildTime = Date.now();
const buildId = crypto
  .createHash('sha256')
  .update(`${pkg.version}-${buildTime}-${Math.random()}`)
  .digest('hex')
  .slice(0, 12);

const versionPayload = {
  appVersion: pkg.version,
  buildId,
  buildTime,
};

fs.writeFileSync(versionOut, JSON.stringify(versionPayload, null, 0));

// Collect production dependencies with resolved versions from lock file
const deps = { ...(pkg.dependencies || {}), ...(pkg.optionalDependencies || {}) };
const components = [];

function findPackageInfo(name) {
  const key = name.startsWith('@') ? `node_modules/${name}` : `node_modules/${name}`;
  let p = lock.packages[key];
  if (p) return p;
  // Try without node_modules
  p = lock.packages[name];
  if (p) return p;
  return null;
}

function normalizeLicense(lic) {
  if (!lic) return null;
  if (typeof lic === 'string') return lic;
  if (lic && typeof lic === 'object' && lic.type) return lic.type;
  return null;
}

for (const [name, range] of Object.entries(deps)) {
  const info = findPackageInfo(name);
  const version = info?.version ?? range;
  components.push({
    name,
    version,
    license: normalizeLicense(info?.license),
    description: info?.description ?? null,
    repository: info?.repository ?? null,
  });
}

components.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

const componentsPayload = {
  generatedAt: buildTime,
  components,
};

fs.writeFileSync(componentsOut, JSON.stringify(componentsPayload, null, 0));

console.log(`Generated ${versionOut} (buildId: ${buildId}) and ${componentsOut} (${components.length} components)`);
