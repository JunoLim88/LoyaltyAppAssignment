# Admin portal

Start the MVC app and visit `/Admin/Login`, or use **Administrator sign in** on the member login page. The current shared API accepts `admin` / `admin` (the integration guide's `admin123` password is outdated). Credentials are verified by the API and are not hardcoded in the application.

## Implemented screens

| Screen | Functions |
| --- | --- |
| Overview | Member, active reward, active voucher and point-record counts; point-status chart; searchable directory linking to record details |
| Members | Search by name/email/ID, view, create, edit, activate/deactivate |
| Rewards | View, create, edit, delete, points, dates, image uploads |
| Vouchers | View, create, edit, delete, images, dates, structured redemption day/time rules |
| Promotions | View, create, edit, delete, images and availability dates |
| Outlets | View, create, activate/deactivate, link to outlet staff |
| Outlet staff | List by outlet, view, create, edit, delete, six-digit PIN management |
| Point records | List, filter by outlet/status, view, assign points, reject a record with staff verification |
| Redemptions | Reward and voucher ledgers and individual redemption details |
| Feedback | List, view, filter by member |

Tables support search, sorting and pagination. Every admin page and controller action checks the server-side admin session. All data requests follow browser → MVC controller → API. Mutations require an antiforgery token. Member passwords, OTPs and tokens are removed from API responses before they reach the browser. Admin and member sessions use separate keys.

The API address is configured with `LoyaltyApi:BaseUrl` in `appsettings.json` (or the `LoyaltyApi__BaseUrl` environment variable). The client handles both the deployed ID-only admin login response and the JWT login response in the supplied API source.

## API constraints

- Point rejection accepts a record GUID and verifying staff details, but has no reason field. The portal requires a reason and records it through `ILogger` on the server. The reason is not stored in, or returned by, the shared API; retain your application logs if you need that audit history.
- The API has no outlet editing/deletion, feedback resolution, or reward-stock endpoints. The portal implements the operations actually provided by the guide/API.
- The API determines promotion status from its expiry date. Its reward/voucher edit implementation also overwrites status based on the record's previous expiry value. A status chosen in the portal can therefore be overridden by the API. The refreshed list displays the saved API result.

## Verification

```powershell
dotnet build --no-restore -o bin/AdminVerification -p:UseAppHost=false
node tests/admin-portal.smoke.cjs
```

The smoke test starts a separate MVC process and an in-memory local HTTP API. It exercises session protection, login/logout, all ten screens, create/edit/delete flows, multipart uploads, voucher-rule identity preservation, point workflows, filtering, paging, input validation, antiforgery protection, credential redaction, error/retry states and mobile widths. It does not write to the shared class API.

The test requires Node.js, Playwright and Chrome. It can use the Codex bundled Playwright package automatically; otherwise install Playwright in your environment or set `PLAYWRIGHT_MODULE_PATH`. Set `CHROME_PATH` when Chrome is elsewhere. Optional `ADMIN_SCREENSHOT_DIR` saves desktop/mobile screenshots.

Live verification completed through the MVC portal: login with the corrected credentials, dashboard loading, and list/detail requests for each admin area. No shared records were created, edited or deleted during live verification.
