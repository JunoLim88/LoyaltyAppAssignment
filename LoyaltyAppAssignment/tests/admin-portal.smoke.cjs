// End-to-end contract checks against an isolated API. Never writes to the shared API.
// Build first: dotnet build --no-restore -o bin/AdminVerification -p:UseAppHost=false
// Run: node tests/admin-portal.smoke.cjs
// Set PLAYWRIGHT_MODULE_PATH and CHROME_PATH if Playwright/Chrome are not on the default paths.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
let playwright;
try { playwright = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright'); }
catch { playwright = require(path.join(process.env.USERPROFILE, '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')); }

const root = path.resolve(__dirname, '..');
const future = '2027-12-31T22:00:00', start = '2026-10-01T09:00:00';
const member = { memberID: 1, memberGUID: 'Member-1', name: 'Test Member', email: 'member@example.test', phoneNumber: '+60123456789', birthDate: '2000-01-01', tier: 'Gold', point: 200, accountStatus: 1, password: 'SECRET_HASH', successToken: 'SECRET_TOKEN', otp: 'SECRET_OTP' };
const outlet = { outletID: 1, outletName: 'Test Branch', outletStatus: 1, createdDate: start };
const staff = { outletStaffID: 1, outletStaffName: 'Test Cashier', pin: '123456', role: 'Cashier', outletID: 1, outlet };
const point = { pointRecordID: 1, pointRecordGUID: 'PointRecord-1', memberID: 1, member, outletStaff: staff, point: 50, pointRecordStatus: 1, dateAssign: start };
const db = {
    members: Array.from({ length: 12 }, (_, i) => ({ ...member, memberID: i + 1, name: i ? 'Test Member ' + (i + 1) : member.name })),
    rewards: [{ rewardsID: 1, name: 'Test Coffee', title: 'A fresh cup', point: 100, image: 'free-coffee.png', status: 'Active', createDate: start, expireDate: future }],
    vouchers: [{ voucherID: 1, voucherName: 'Test Voucher', title: 'Coffee on us', image: 'free-coffee.png', voucherStatus: 'Active', createdDate: start, expiredDate: future, voucherRules: [{ voucherRuleID: 9, voucherID: 1, dayOfWeek: 'Monday', startTime: '09:00:00', endTime: '22:00:00' }] }],
    highlights: [{ highlightID: 1, title: 'Test Promotion', image: 'happy-hour.png', status: 'Active', createDate: start, expireDate: future }],
    outlets: [outlet], staff: [staff], points: [point],
    rewardRecords: [{ rewardRecordID: 1, rewardName: 'Test Coffee', memberName: 'Test Member', outletName: 'Test Branch', memberRedeemDateTime: 1790000000000 }],
    voucherRecords: [{ voucherRecordID: 1, voucherName: 'Test Voucher', memberName: 'Test Member', outletName: 'Test Branch', memberRedeemDateTime: 1790000000000 }],
    feedback: [{ feedbackID: 1, title: 'A lovely visit', description: '<script>unsafe()</script>', rating: '5', category: 'Service', location: 'Test Branch', memberID: 1 }]
};
const ids = { members: 'memberID', rewards: 'rewardsID', vouchers: 'voucherID', highlights: 'highlightID', outlets: 'outletID', staff: 'outletStaffID', points: 'pointRecordID', rewardRecords: 'rewardRecordID', voucherRecords: 'voucherRecordID', feedback: 'feedbackID' };
const listActions = { GetAllMember: 'members', GetAllRewards: 'rewards', GetAllVouchers: 'vouchers', GetAllHighlight: 'highlights', GetAllOutlets: 'outlets', GetAllOutletStaff: 'staff', GetAllPointAssignRecord: 'points', GetAllRewardAssignRecord: 'rewardRecords', GetAllVoucherAssignRecord: 'voucherRecords', GetAllFeedbackList: 'feedback' };
const detailActions = { FindMemberById: 'members', GetReward: 'rewards', GetVoucher: 'vouchers', GetHighlight: 'highlights', GetOutlet: 'outlets', GetOutletStaff: 'staff', GetAllPointAssignRecordByPointId: 'points', GetRewardAssignRecordById: 'rewardRecords', GetVoucherAssignRecordById: 'voucherRecords', GetFeedback: 'feedback' };
let requireJwt = false, failNext = null;
const calls = [];

function multipart(body, contentType) {
    const boundary = contentType.match(/boundary="?([^";]+)/)[1];
    const result = {};
    for (const part of body.toString().split('--' + boundary)) {
        const name = part.match(/name="?([^";\r\n]+)"?/i)?.[1];
        const split = part.indexOf('\r\n\r\n');
        if (name && split >= 0) result[name] = part.slice(split + 4).replace(/\r\n$/, '');
    }
    return result;
}
function lowerKeys(obj) { return Object.fromEntries(Object.entries(obj).map(([k, v]) => [k.toLowerCase(), v])); }
function reply(res, data, code = 200) { res.writeHead(code, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data)); }

const api = http.createServer(async (req, res) => {
    try {
        const chunks = []; for await (const c of req) chunks.push(c);
        const body = Buffer.concat(chunks), url = new URL(req.url, 'http://fixture');
        const [, , controller, action, routeId] = url.pathname.split('/');
        const form = req.headers['content-type']?.startsWith('multipart/') ? multipart(body, req.headers['content-type']) : null;
        const data = form || (body.length ? JSON.parse(body.toString()) : {}), input = lowerKeys(data);
        calls.push({ controller, action, method: req.method, input, form, auth: req.headers.authorization });
        if (action === 'AdminLogin') {
            if (input.password !== 'admin') return reply(res, { message: 'Invalid username or password.' }, 401);
            requireJwt = input.username === 'jwt';
            return reply(res, requireJwt ? { AdminID: 1, SuccessToken: 'fixture-jwt' } : { message: 'Login successful', adminID: 1, username: 'admin' });
        }
        if (requireJwt && req.headers.authorization !== 'Bearer fixture-jwt') return reply(res, 'Missing admin token', 401);
        if (failNext === action) { failNext = null; return reply(res, { message: 'Fixture API unavailable' }, 503); }
        if (listActions[action]) {
            const k = listActions[action];
            return reply(res, k === 'staff' ? db.staff.filter(r => r.outletID === Number(input.outletid)) : db[k]);
        }
        if (detailActions[action]) {
            const k = detailActions[action], id = Number(routeId || url.searchParams.get('id') || input.outletid);
            return reply(res, db[k].find(r => r[ids[k]] === id) || null);
        }
        if (action === 'GetAllPointAssignRecordByOutletId') return reply(res, db.points.filter(p => p.outletStaff?.outletID === Number(routeId)));
        if (action === 'GetFeedbackListFilterUser') {
            const records = db.feedback.filter(r => r.memberID === Number(input.memberid));
            return reply(res, records.length ? records : 'No Records Found', records.length ? 200 : 400);
        }
        if (action === 'ActiveOrDeactiveMember' || action === 'ActiveOrDeactiveOutlet') {
            const k = action.endsWith('Member') ? 'members' : 'outlets', id = Number(input.memberid || routeId);
            const record = db[k].find(r => r[ids[k]] === id), key = k === 'members' ? 'accountStatus' : 'outletStatus';
            record[key] = record[key] === 1 ? 2 : 1; return reply(res, 'Update Successful');
        }
        if (/^Delete/.test(action)) {
            const k = { DeleteReward: 'rewards', DeleteVoucher: 'vouchers', DeleteHighlight: 'highlights', DeleteOutletStaff: 'staff' }[action];
            assert.equal(req.method, k === 'staff' ? 'DELETE' : 'GET');
            db[k] = db[k].filter(r => r[ids[k]] !== Number(routeId)); return reply(res, 'Deleted successfully');
        }
        if (action === 'OutletAssignPoint') { assert.equal(input.point, 25); return reply(res, 'Points assigned'); }
        if (action === 'RejectPoint') {
            assert.equal(input.id, 'PointRecord-1'); assert.equal(input.assignpoint.point, 0); assert.equal(input.assignpoint.phoneNumber, '0123456789');
            db.points[0].pointRecordStatus = 2; return reply(res, 'Rejected');
        }
        const saves = { CreateMember: 'members', EditMember: 'members', CreateReward: 'rewards', EditReward: 'rewards', CreateVoucher: 'vouchers', EditVoucher: 'vouchers', CreateHighlight: 'highlights', EditHighlight: 'highlights', PostOutlet: 'outlets', CreateOutletStaff: 'staff', UpdateOutletStaff: 'staff' };
        if (saves[action]) {
            const k = saves[action];
            const id = Number(input.id || input.memberid || input.voucherid || input.highlightid || input.outletstaffid || 0);
            let record = id ? db[k].find(r => r[ids[k]] === id) : { [ids[k]]: Math.max(0, ...db[k].map(r => r[ids[k]])) + 1 };
            if (['rewards', 'vouchers', 'highlights'].includes(k)) {
                assert.ok(form, 'Catalog writes must use multipart/form-data');
                if (action === 'CreateReward') assert.ok(input.fullname);
                if (action === 'EditReward') assert.ok(input.id);
                if (action === 'CreateHighlight') assert.ok(input.file, 'Promotion image must reach API');
            }
            Object.assign(record, { name: input.name || input.fullname, title: input.title, description: input.description, email: input.email, phoneNumber: input.phonenumber, birthDate: input.birthdate, tier: input.tier, voucherName: input.vouchername, outletName: input.outletname, outletStaffName: input.outletstaffname, outletID: Number(input.outletid || 1), role: input.role, pin: input.pin, point: Number(input.point || 0), createDate: input.createdate, expireDate: input.expiredate, createdDate: input.createddate, expiredDate: input.expireddate, status: input.status || 'Active', voucherStatus: 'Active', accountStatus: 1, outletStatus: 1 });
            if (input.voucherrules) record.voucherRules = JSON.parse(input.voucherrules);
            if (!id) db[k].push(record);
            return reply(res, 'Changes saved');
        }
        reply(res, 'Fixture endpoint missing: ' + action, 404);
    } catch (error) { console.error(error); reply(res, error.message, 500); }
});

async function main() {
    await new Promise(resolve => api.listen(0, '127.0.0.1', resolve));
    const apiPort = api.address().port;
    const socket = http.createServer(); await new Promise(resolve => socket.listen(0, '127.0.0.1', resolve));
    const appPort = socket.address().port; await new Promise(resolve => socket.close(resolve));
    const origin = 'http://127.0.0.1:' + appPort;
    const app = spawn('dotnet', [path.join(root, 'bin/AdminVerification/LoyaltyAppAssignment.dll')], { cwd: root, env: { ...process.env, ASPNETCORE_URLS: origin, ASPNETCORE_ENVIRONMENT: 'Development', LoyaltyApi__BaseUrl: 'http://127.0.0.1:' + apiPort + '/' }, stdio: ['ignore', 'pipe', 'pipe'] });
    let logs = ''; app.stdout.on('data', c => { logs += c; }); app.stderr.on('data', c => { logs += c; });
    let browser;
    try {
        for (let i = 0; i < 100; i++) {
            try { if ((await fetch(origin + '/Admin/Login')).ok) break; } catch {}
            if (i === 99) throw new Error('App did not start: ' + logs);
            await new Promise(resolve => setTimeout(resolve, 100));
        }
        browser = await playwright.chromium.launch({ headless: true, executablePath: process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
        const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
        const page = await context.newPage(), errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await context.route('https://fonts.**/**', r => r.abort());
        await context.route('https://alpacaapi.xcode.com.my/**', r => r.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="600" height="400"><rect width="600" height="400" fill="#b99c7b"/></svg>' }));
        const pages = ['Dashboard', 'Members', 'Rewards', 'Vouchers', 'Highlights', 'Outlets', 'Staff', 'Points', 'Redemptions', 'Feedback'];
        for (const name of pages) { const r = await context.request.get(origin + '/Admin/' + name, { maxRedirects: 0 }); assert.equal(r.status(), 302, name + ' must require login'); }
        assert.equal((await context.request.get(origin + '/Admin/Data?section=members', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })).status(), 401);
        await page.goto(origin + '/Admin/Login');
        await page.locator('#Username').fill('admin'); await page.locator('#Password').fill('wrong');
        await page.locator('button[type=submit]').click(); await page.locator('#loginMessage:visible').waitFor();
        assert.match(await page.locator('#loginMessage').innerText(), /Invalid/);
        await page.locator('#Password').fill('admin'); await page.locator('button[type=submit]').click(); await page.waitForURL('**/Admin/Dashboard');
        await page.locator('.metric').first().waitFor(); assert.equal(await page.locator('.metric').count(), 4);
        assert.equal(await page.locator('.chart-row').count(), 3);
        await page.locator('#searchInput').fill('Test Member'); assert.ok(await page.locator('#recordsTable tbody tr').count() > 0);
        const screenshotDir = process.env.ADMIN_SCREENSHOT_DIR;
        if (screenshotDir) { fs.mkdirSync(screenshotDir, { recursive: true }); await page.locator('#searchInput').fill(''); await page.screenshot({ path: path.join(screenshotDir, 'admin-dashboard.png'), fullPage: true }); }
        console.log('PASS: login, session guards, dashboard metrics/chart/search');

        async function navigate(name) { await page.goto(origin + '/Admin/' + name); await page.waitForFunction(() => document.querySelector('#loading').hidden); }
        async function create() { await page.locator('#createButton').click(); await page.locator('#editorDialog[open]').waitFor(); }
        async function save() {
            await page.locator('#saveButton').click();
            await page.waitForFunction(() => !document.querySelector('#editorDialog').open || (!document.querySelector('#editorMessage').hidden && !document.querySelector('#saveButton').disabled));
            assert.equal(await page.locator('#editorDialog').evaluate(e => e.open), false, await page.locator('#editorMessage').innerText());
        }
        async function rowAction(name, action) { const row = page.locator('#recordsTable tbody tr').filter({ hasText: name }).first(); await row.getByRole('button', { name: action, exact: true }).click(); }
        async function confirm() { await page.locator('#confirmButton').click(); await page.locator('#confirmDialog').waitFor({ state: 'hidden' }); }
        async function fillCatalog(name) { if (await page.locator('#fieldName').count()) await page.locator('#fieldName').fill(name); await page.locator('#fieldTitle').fill(name); }
        const upload = { name: 'test.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+nmS8AAAAASUVORK5CYII=', 'base64') };

        await navigate('Members');
        assert.match(await page.locator('#pageInfo').innerText(), /of 12/); await page.locator('#nextPage').click(); assert.match(await page.locator('#pageInfo').innerText(), /11–12/);
        await page.locator('#searchInput').fill('member@example.test'); assert.ok(await page.locator('#recordsTable tbody tr').count() > 0);
        const secretResponse = await context.request.get(origin + '/Admin/Data?section=members');
        assert.doesNotMatch(await secretResponse.text(), /SECRET_HASH|SECRET_TOKEN|SECRET_OTP/);
        await create();
        await page.locator('#fieldName').fill('Smoke Member'); await page.locator('#fieldEmail').fill('smoke@example.test'); await page.locator('#fieldPhoneNumber').fill('0129999999'); await page.locator('#fieldBirthDate').fill('2000-02-02'); await page.locator('#fieldPassword').fill('testpass'); await save();
        await page.locator('#searchInput').fill('Smoke Member'); await rowAction('Smoke Member', 'Edit'); await page.locator('#fieldTier').fill('Platinum'); await save();
        await rowAction('Smoke Member', 'Deactivate'); await confirm(); assert.equal(db.members.at(-1).accountStatus, 2);
        const token = await page.locator('#editorForm input[name=__RequestVerificationToken]').inputValue();
        assert.equal((await context.request.post(origin + '/Admin/SaveMember', { form: { Name: 'Invalid' }, headers: { RequestVerificationToken: token } })).status(), 400);
        assert.equal((await context.request.post(origin + '/Admin/Delete', { form: { kind: 'rewards', id: '1' } })).status(), 400);
        console.log('PASS: member create/edit/status, paging, search, credential redaction, server validation, CSRF');

        for (const [name, k] of [['Rewards', 'rewards'], ['Vouchers', 'vouchers'], ['Highlights', 'highlights']]) {
            await navigate(name); await create(); await fillCatalog('Smoke ' + name);
            await page.locator('#fieldFile').setInputFiles(upload);
            if (k === 'vouchers') { await page.getByRole('button', { name: '+ Add day' }).click(); }
            await save();
            const last = calls.findLast(c => /^Create/.test(c.action) && c.form && c.input.title === 'Smoke ' + name);
            assert.ok(last?.input.file, 'Image upload must be forwarded');
            await rowAction('Smoke ' + name, 'Edit'); await page.locator('#fieldTitle').fill('Updated ' + name); await save();
            if (k === 'vouchers') {
                await rowAction('Test Voucher', 'Edit'); await save();
                assert.equal(JSON.parse(calls.findLast(c => c.action === 'EditVoucher').input.voucherrules)[0].VoucherRuleID, 9, 'Existing voucher rule identity must survive editing');
            }
            const rowName = k === 'highlights' ? 'Updated ' + name : 'Smoke ' + name;
            await rowAction(rowName, 'View'); await page.locator('#detailsDialog[open]').waitFor(); await page.locator('#detailsDialog .close-dialog').first().click();
            await rowAction(rowName, 'Delete'); await confirm();
            assert.equal(db[k].length, 1);
        }
        console.log('PASS: reward/voucher/promotion CRUD, multipart images, API field mappings, voucher rule identities');

        await navigate('Outlets'); await create(); await page.locator('#fieldOutletName').fill('Smoke Branch'); await save();
        await rowAction('Smoke Branch', 'Deactivate'); await confirm();
        await rowAction('Test Branch', 'View'); await page.locator('#detailsDialog[open]').waitFor(); await page.locator('#detailsDialog .close-dialog').first().click();
        await navigate('Staff'); await create(); await page.locator('#fieldOutletStaffName').fill('Smoke Staff'); await page.locator('#fieldPin').fill('654321'); await page.locator('#fieldRole').fill('Cashier'); await save();
        await rowAction('Smoke Staff', 'Edit'); await page.locator('#fieldRole').fill('Supervisor'); await save();
        await rowAction('Smoke Staff', 'Delete'); await confirm(); assert.equal(db.staff.length, 1);
        console.log('PASS: outlet creation/status/details and outlet-scoped staff CRUD');

        await navigate('Points'); await create(); await page.locator('#fieldPhoneNumber').fill('0123456789'); await page.locator('#fieldPoint').fill('25'); await page.locator('#fieldOutletID').selectOption('1'); await page.locator('#fieldPin').fill('123456'); await save();
        await rowAction('50', 'Reject'); await page.locator('#fieldPin').fill('123456'); await page.locator('#fieldReason').fill('Smoke test reversal'); await save();
        assert.equal(db.points[0].pointRecordStatus, 2);
        assert.match(logs, /Smoke test reversal/);
        await page.locator('#outletFilter').selectOption('1'); await page.waitForFunction(() => document.querySelector('#loading').hidden);
        await navigate('Redemptions'); await rowAction('Test Coffee', 'View'); await page.locator('#detailsDialog[open]').waitFor(); await page.locator('#detailsDialog .close-dialog').first().click();
        await page.locator('#typeFilter').selectOption('voucherRecords'); await page.getByText('Test Voucher', { exact: true }).waitFor();
        await navigate('Feedback'); await rowAction('A lovely visit', 'View'); await page.locator('#detailsDialog[open]').waitFor(); assert.match(await page.locator('#detailsContent').innerText(), /<script>unsafe/); await page.locator('#detailsDialog .close-dialog').first().click();
        await page.locator('#memberFilter').selectOption('2'); await page.locator('#emptyState:visible').waitFor(); assert.equal(await page.locator('#recordsTable tbody tr').count(), 0);
        console.log('PASS: point assignment/rejection/audit, outlet filters, both redemption ledgers, feedback member filter and safe detail text');

        for (const name of pages) {
            await navigate(name); await page.setViewportSize({ width: 390, height: 844 });
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, name + ' must fit mobile viewport');
        }
        if (screenshotDir) await page.screenshot({ path: path.join(screenshotDir, 'admin-mobile.png'), fullPage: true });
        await page.setViewportSize({ width: 1440, height: 1000 });
        failNext = 'GetAllMember'; await navigate('Members'); await page.locator('#pageMessage:visible').waitFor(); assert.match(await page.locator('#pageMessage').innerText(), /unavailable/);
        await page.locator('#refreshButton').click(); await page.waitForFunction(() => document.querySelector('#loading').hidden); assert.ok(await page.locator('#recordsTable tbody tr').count());
        const logoutResponse = page.waitForResponse(r => r.url().endsWith('/Admin/Logout'));
        await page.locator('.admin-sidebar .signout').click();
        const signedOut = await logoutResponse;
        assert.equal(signedOut.status(), 302, 'Logout should redirect to sign in');
        await page.waitForURL(url => /^\/Admin(?:\/Login)?$/.test(url.pathname));
        await page.locator('#Username').fill('jwt'); await page.locator('#Password').fill('admin'); await page.locator('button[type=submit]').click(); await page.waitForURL('**/Admin/Dashboard'); await page.locator('.metric').first().waitFor();
        assert.ok(calls.some(c => c.auth === 'Bearer fixture-jwt'));
        assert.deepEqual(errors, []);
        console.log('PASS: all ten screens responsive, API failure/retry, logout, JWT-compatible login, zero JavaScript errors');
    } finally {
        if (browser) await browser.close();
        app.kill(); api.close();
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; api.close(); });
