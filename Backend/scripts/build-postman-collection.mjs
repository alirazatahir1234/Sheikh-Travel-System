#!/usr/bin/env node
/**
 * Builds SheikhTravelSystem.API.postman_collection.json from the live OpenAPI spec.
 *
 * Usage:
 *   node Backend/scripts/build-postman-collection.mjs [swagger-url-or-path]
 *
 * Default swagger URL: https://localhost:7012/swagger/v1/swagger.json
 */

import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../..');
const require = createRequire(import.meta.url);

const swaggerSource = process.argv[2] ?? 'https://localhost:7012/swagger/v1/swagger.json';
const tmpDir = path.join(__dirname, '.postman-build');
const swaggerPath = path.join(tmpDir, 'swagger.json');
const rawCollectionPath = path.join(tmpDir, 'raw.postman_collection.json');

const OUT_PATHS = [
  path.join(repoRoot, 'Backend/docs/SheikhTravelSystem.API.postman_collection.json'),
  path.join(repoRoot, 'Frontend/sheikh-travel-customer-hub/public/postman/SheikhTravelSystem.API.postman_collection.json')
];

const FOLDER_LABELS = {
  platform: 'Platform Admin',
  'customer-portal': 'Customer Hub',
  dev: 'Dev (local only)',
  'driver-app': 'Driver App',
  gps: 'GPS Tracking',
  lookup: 'Lookup',
  settings: 'Settings',
  tenants: 'Tenants',
  tracking: 'Tracking (legacy)',
  users: 'User Profile',
  Users: 'Users (Admin)',
  DriverAllowanceRules: 'Driver Allowance Rules',
  FuelLogs: 'Fuel Logs',
  Ocr: 'OCR'
};

const NO_AUTH_PREFIXES = [
  '/api/auth/login',
  '/api/auth/refresh-token',
  '/api/customer-portal/auth/',
  '/api/customer-portal/routes',
  '/api/customer-portal/vehicles',
  '/api/customer-portal/price-estimate',
  '/api/customer-portal/quote',
  '/api/customer-portal/promo/validate',
  '/api/customer-portal/bookings',
  '/api/customer-portal/my-bookings',
  '/api/customer-portal/payment-gateway',
  '/api/customer-portal/payments/webhook/',
  '/api/driver-app/auth/login',
  '/api/tenants/branding',
  '/api/dev/'
];

const SAMPLE_BODIES = {
  'POST /api/auth/login': {
    email: 'admin@sheikhtravel.com',
    password: 'Admin@123'
  },
  'POST /api/auth/refresh-token': {
    refreshToken: '{{refreshToken}}'
  },
  'POST /api/vehicles': {
    vehicle: {
      name: 'Toyota Hiace',
      registrationNumber: 'LEA-1234',
      vehicleCode: 'V-001',
      vin: 'JT123456789012345',
      make: 'Toyota',
      model: 'High Roof',
      year: 2022,
      color: 'White',
      vehicleType: 'Van',
      seatingCapacity: 14,
      fuelAverage: 9.5,
      fuelType: 2,
      engineNo: 'ENG-001',
      chassisNo: 'CHS-001',
      currentMileage: 45000,
      insuranceExpiryDate: '2027-12-31T00:00:00Z',
      purchaseDate: '2022-01-15T00:00:00Z',
      purchasePrice: 4500000,
      branchId: 1,
      departmentId: null
    }
  },
  'PUT /api/vehicles/:id': {
    id: 1,
    vehicle: {
      name: 'Toyota Hiace',
      registrationNumber: 'LEA-1234',
      vehicleCode: 'V-001',
      make: 'Toyota',
      model: 'High Roof',
      year: 2022,
      seatingCapacity: 14,
      fuelAverage: 9.8,
      fuelType: 2,
      currentMileage: 46000,
      insuranceExpiryDate: '2027-12-31T00:00:00Z',
      status: 1
    }
  },
  'POST /api/vehicles/{id}/documents': {
    documentType: 'Registration',
    fileUrl: 'https://example.com/docs/registration.pdf',
    expiryDate: '2027-12-31T00:00:00Z',
    notes: 'Annual renewal'
  },
  'POST /api/vehicles/{id}/change-status': {
    status: 2,
    reason: 'Assigned to trip'
  },
  'POST /api/vehicles/{id}/assign-driver': {
    driverId: 1,
    bookingId: null,
    assignmentType: 'Trip'
  },
  'POST /api/vehicles/{id}/assign-gps': {
    gpsDeviceId: 1
  },
  'POST /api/customer-portal/bookings': {
    fullName: 'Portal Test',
    phone: '{{portalPhone}}',
    email: 'portal@example.com',
    routeId: 1,
    vehicleId: 1,
    pickupTime: '2026-12-01T10:00:00.000Z',
    passengerCount: 2,
    isRoundTrip: false,
    notes: null,
    paymentPlan: 'payLater',
    initialPaymentAmount: null
  },
  'POST /api/customer-portal/price-estimate': {
    routeId: 1,
    vehicleId: 1,
    isRoundTrip: false
  }
};

const LOGIN_TEST_SCRIPT = [
  "pm.test('Status code is 200', function () {",
  "    pm.response.to.have.status(200);",
  '});',
  '',
  'const json = pm.response.json();',
  'if (json && json.data) {',
  "    if (json.data.accessToken) pm.collectionVariables.set('accessToken', json.data.accessToken);",
  "    if (json.data.refreshToken) pm.collectionVariables.set('refreshToken', json.data.refreshToken);",
  '}'
];

const REFRESH_TEST_SCRIPT = [
  'const json = pm.response.json();',
  'if (json && json.data) {',
  "    if (json.data.accessToken) pm.collectionVariables.set('accessToken', json.data.accessToken);",
  "    if (json.data.refreshToken) pm.collectionVariables.set('refreshToken', json.data.refreshToken);",
  '}'
];

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function fetchSwagger(source) {
  if (source.startsWith('http://') || source.startsWith('https://')) {
    execSync(`curl -sk "${source}" -o "${swaggerPath}"`, { stdio: 'inherit' });
  } else {
    fs.copyFileSync(path.resolve(source), swaggerPath);
  }
  if (!fs.existsSync(swaggerPath) || fs.statSync(swaggerPath).size < 1000) {
    throw new Error(`Failed to load swagger spec from ${source}`);
  }
}

function convertToPostman() {
  execSync(
    `npx --yes openapi-to-postmanv2 -s "${swaggerPath}" -o "${rawCollectionPath}" -p`,
    { stdio: 'inherit', cwd: repoRoot }
  );
}

function requestPath(item) {
  const parts = item?.request?.url?.path;
  if (!Array.isArray(parts)) return '';
  return '/' + parts.join('/');
}

function normalizedPath(item) {
  return requestPath(item).toLowerCase();
}

function requestKey(item) {
  const method = item?.request?.method ?? 'GET';
  const p = normalizedPath(item).replace(/\/:\w+/g, '/{id}');
  return `${method} ${p}`;
}

function shouldNoAuth(item) {
  const p = normalizedPath(item);
  return NO_AUTH_PREFIXES.some((prefix) => p.startsWith(prefix));
}

function humanizeRequestName(item) {
  const method = item?.request?.method ?? 'GET';
  const p = requestPath(item);
  const segments = p.split('/').filter(Boolean);
  const resource = segments.slice(1).join(' / ') || p;
  return `${method} ${resource}`;
}

function applySampleBody(item) {
  const key = requestKey(item);
  const body = SAMPLE_BODIES[key];
  if (!body) return;
  item.request.header = item.request.header ?? [];
  if (!item.request.header.some((h) => h.key?.toLowerCase() === 'content-type')) {
    item.request.header.push({ key: 'Content-Type', value: 'application/json' });
  }
  item.request.body = {
    mode: 'raw',
    raw: JSON.stringify(body, null, 2)
  };
}

function walkItems(items, fn) {
  if (!Array.isArray(items)) return;
  for (const item of items) {
    fn(item);
    if (item.item) walkItems(item.item, fn);
  }
}

function renameFolders(items) {
  for (const folder of items) {
    if (FOLDER_LABELS[folder.name]) {
      folder.name = FOLDER_LABELS[folder.name];
    }
    if (folder.item) renameFolders(folder.item);
  }
}

function sortFolders(items) {
  const order = [
    'Auth',
    'Dashboard',
    'Fleet',
    'Vehicles',
    'Drivers',
    'Driver App',
    'Bookings',
    'Routes',
    'Customers',
    'Maintenance',
    'Fuel Logs',
    'GPS Tracking',
    'Tracking (legacy)',
    'Payments',
    'Pricing',
    'Reports',
    'Notifications',
    'AuditLogs',
    'Users (Admin)',
    'User Profile',
    'Driver Allowance Rules',
    'Settings',
    'Lookup',
    'Tenants',
    'Platform Admin',
    'OCR',
    'Customer Hub',
    'Dev (local only)'
  ];
  items.sort((a, b) => {
    const ai = order.indexOf(a.name);
    const bi = order.indexOf(b.name);
    if (ai === -1 && bi === -1) return a.name.localeCompare(b.name);
    if (ai === -1) return 1;
    if (bi === -1) return -1;
    return ai - bi;
  });
}

function flattenFolder(folder) {
  const requests = [];
  const walk = (items) => {
    for (const entry of items ?? []) {
      if (entry.request) requests.push(entry);
      else if (entry.item) walk(entry.item);
    }
  };
  walk(folder.item);
  return {
    name: folder.name,
    description: folder.description ?? '',
    item: requests
  };
}

function postProcess(raw) {
  const apiRoot = raw.item?.find((i) => i.name === 'api');
  const folders = (apiRoot?.item ?? raw.item ?? []).map(flattenFolder);

  renameFolders(folders);
  sortFolders(folders);
  for (const folder of folders) {
    folder.item.sort((a, b) => a.name.localeCompare(b.name));
  }

  walkItems(folders, (item) => {
    if (!item.request) return;

    item.name = humanizeRequestName(item);

    if (item.request.url?.host) {
      item.request.url.host = ['{{baseUrl}}'];
    }

    if (shouldNoAuth(item)) {
      item.request.auth = { type: 'noauth' };
    }

    applySampleBody(item);

    const p = normalizedPath(item);
    if (p === '/api/auth/login') {
      item.event = [{
        listen: 'test',
        script: { type: 'text/javascript', exec: LOGIN_TEST_SCRIPT }
      }];
    }
    if (p === '/api/auth/refresh-token') {
      item.event = [{
        listen: 'test',
        script: { type: 'text/javascript', exec: REFRESH_TEST_SCRIPT }
      }];
    }
  });

  return {
    info: {
      _postman_id: '8f0b86b0-a9f6-4d59-b1d9-6e31be42ea55',
      name: 'Sheikh Travel System API',
      description:
        'Full Postman collection generated from the live OpenAPI spec.\n\n' +
        '1. Set `baseUrl` (default http://localhost:5082).\n' +
        '2. Run **Auth → POST api / auth / login** to save `accessToken` and `refreshToken`.\n' +
        '3. All other admin endpoints inherit Bearer auth from the collection.\n' +
        '4. Customer Hub folder uses `noauth` for public portal calls; set `portalPhone`.\n\n' +
        'Regenerate: `node Backend/scripts/build-postman-collection.mjs`',
      schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'
    },
    auth: {
      type: 'bearer',
      bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }]
    },
    variable: [
      { key: 'baseUrl', value: 'http://localhost:5082' },
      { key: 'accessToken', value: '' },
      { key: 'refreshToken', value: '' },
      { key: 'portalPhone', value: '+923001234567' },
      { key: 'vehicleId', value: '1' },
      { key: 'driverId', value: '1' },
      { key: 'bookingId', value: '1' },
      { key: 'tenantId', value: '1' }
    ],
    item: folders
  };
}

function main() {
  ensureDir(tmpDir);
  console.log(`Fetching OpenAPI spec from ${swaggerSource}...`);
  fetchSwagger(swaggerSource);
  console.log('Converting OpenAPI → Postman...');
  convertToPostman();
  const raw = JSON.parse(fs.readFileSync(rawCollectionPath, 'utf8'));
  const collection = postProcess(raw);

  for (const out of OUT_PATHS) {
    ensureDir(path.dirname(out));
    fs.writeFileSync(out, JSON.stringify(collection, null, 2) + '\n');
    console.log(`Wrote ${out}`);
  }

  const envPath = path.join(repoRoot, 'Backend/docs/SheikhTravelSystem.API.postman_environment.json');
  fs.writeFileSync(envPath, JSON.stringify({
    id: 'sts-local-env',
    name: 'Sheikh Travel — Local',
    values: [
      { key: 'baseUrl', value: 'http://localhost:5082', enabled: true },
      { key: 'accessToken', value: '', enabled: true },
      { key: 'refreshToken', value: '', enabled: true },
      { key: 'portalPhone', value: '+923001234567', enabled: true }
    ],
    _postman_variable_scope: 'environment'
  }, null, 2) + '\n');
  console.log(`Wrote ${envPath}`);

  const count = { folders: 0, requests: 0 };
  const countItems = (items) => {
    for (const i of items) {
      if (i.request) count.requests++;
      else if (i.item) { count.folders++; countItems(i.item); }
    }
  };
  countItems(collection.item);
  console.log(`Done: ${count.folders} folders, ${count.requests} requests.`);
}

main();
