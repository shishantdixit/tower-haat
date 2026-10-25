# Deploying TowerHaat on MonsterASP

Two sites on the same plan: **staging** (`staging.towerhaat.com`) to try every release first, and
**production** (`towerhaat.com`). Each has its own SQL Server database. The app is one ASP.NET Core
site that also serves the Angular app, so there is nothing else to host.

> MonsterASP's panel changes from time to time. Where a menu name below does not match, look for
> the same thing under a similar name, or ask their support.

## 1. One-time setup (per site: do staging first, then production)

### Site and .NET
1. In the MonsterASP panel, create a website. Production: `towerhaat.com`. Staging: add the
   subdomain `staging.towerhaat.com` as its own site.
2. Set the site to **.NET (Core) / ASP.NET Core** with **.NET 10**. If the panel does not offer
   .NET 10, deploy with *Bundle the .NET runtime* ticked (see section 3); it then runs without it.

### Database
3. Create a **SQL Server (MSSQL)** database for the site. Note the server, database name, user and
   password. The app creates all its tables itself on first start.

### Domain and HTTPS
4. Point the domain's DNS at MonsterASP as the panel tells you (usually an A record for
   `towerhaat.com` and `www`, and one for `staging`). Changes can take a few hours.
5. In the panel, issue a free **Let's Encrypt** certificate for each host name and turn on
   **force HTTPS** if offered. The app also redirects to HTTPS and sends HSTS in production.

### Settings (secrets)
6. Make the secrets. Run these on your computer from the repository folder:

   ```bash
   dotnet run --project src/TowerHaat.Api -- generate-vapid-keys   # push keys, once per site
   openssl rand -base64 48                                          # run three times: JWT, OTP and job keys
   ```

7. Create `appsettings.Production.json` (production) or `appsettings.Staging.json` (staging) from
   this template, and upload it to the site's root folder (next to `TowerHaat.Api.dll`) with the
   panel's File Manager or FTP. Deploys never overwrite or delete it, and it is never committed.

   ```json
   {
     "ConnectionStrings": {
       "Default": "Server=<server>;Database=<database>;User Id=<user>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=False"
     },
     "Database": { "MigrateOnStartup": true },
     "Jwt": { "SigningKey": "<random 1>" },
     "Auth": {
       "AdminPhones": [ "<your mobile number>" ],
       "Otp": { "HashKey": "<random 2>", "ExposeCodeInResponse": false }
     },
     "Jobs": { "Key": "<random 3>" },
     "Push": {
       "PublicKey": "<Push__PublicKey from generate-vapid-keys>",
       "PrivateKey": "<Push__PrivateKey>",
       "Subject": "mailto:support@towerhaat.com"
     }
   }
   ```

   On **staging**, set `"ExposeCodeInResponse": true` while SMS is not live: the sign-in screen
   then shows the code. Production never shows it, whatever the setting.

   Keep the push keys the same forever: new keys make every phone turn notifications on again.

### Web Deploy credentials in GitHub
8. In the panel, enable **Web Deploy** for the site and download or view its publish settings.
   You need: the Web Deploy URL (looks like `https://<server>:8172/msdeploy.axd?site=<site>`), the
   site name, the user name and the password.
9. In GitHub: **Settings → Environments → New environment**, named `staging` (and later
   `production`). Add these **secrets**: `WEBDEPLOY_URL`, `WEBDEPLOY_SITE`, `WEBDEPLOY_USERNAME`,
   `WEBDEPLOY_PASSWORD`; and this **variable**: `SITE_URL` (`https://staging.towerhaat.com`).
   For production, also tick **Required reviewers** and add yourself, so nothing reaches
   production by accident.

## 2. Scheduled tasks

In the panel's **Scheduled tasks** (cron / URL calls), add two tasks per site. Use the Jobs key
from the settings file in place of `<key>`.

| Task | URL | When |
| --- | --- | --- |
| Expire and remind orders | `https://towerhaat.com/api/jobs/expire-orders?key=<key>` | every 5 minutes |
| Database backup | `https://towerhaat.com/api/jobs/backup?key=<key>` | daily, 03:00 |

A plain GET is enough. If the panel can send headers, prefer `POST` with the header
`X-Job-Key: <key>` and no `?key=`, which keeps the key out of web server logs. The first task
also wakes the site if IIS put it to sleep. A wrong key answers 404.

## 3. Deploying a version

1. GitHub → **Actions → Deploy → Run workflow**.
2. Choose the branch (or tag), the environment (`staging` first), and leave *Bundle the .NET
   runtime* unticked unless the site showed **HTTP 500.31** last time.
3. The workflow runs all tests, builds the app, copies it with Web Deploy (the site shows
   "offline" for a few seconds), then checks `/api/health`. Database changes are applied when the
   site starts.
4. Try the release on staging with your phone. Then run the same workflow for `production`.

**Rolling back:** run the workflow again on the previous commit or tag. Database changes are
never rolled back automatically, so releases only ever add columns or tables.

**From your Windows PC instead of GitHub:** run `publish.cmd` (or `publish.cmd production`) from
the repository folder. It does the same steps: tests, app build, `dotnet publish`, Web Deploy with
the same rules (keeps `App_Data` and the settings file), then the `/api/health` check. Download
the site's **Web Deploy publish profile** from the panel and save it next to `publish.cmd` as
`staging.PublishSettings` or `production.PublishSettings`; the script reads the server, site, user,
password and site address from it. (Without a profile, the first run writes
`publish.<env>.local.cmd` to fill in by hand.) Both hold the password, are gitignored and must
never be committed. Options: `/skip-tests`, and `/self-contained` if the site showed HTTP 500.31.
Production asks you to type `yes` first. Needs the .NET SDK, Node.js and
[Web Deploy](https://www.iis.net/downloads/microsoft/web-deploy) (`choco install webdeploy`).

**If the site shows HTTP 500.30** (the app failed to start): open `App_Data/logs/startup-error.log`
in the panel's File Manager. The last entry names the cause, usually a missing or short setting in
`appsettings.Production.json` or a database that cannot be reached. Fix it, then restart the site.

**If Web Deploy will not connect:** each run keeps the built app as an artifact for 30 days
(*towerhaat-…* on the run's page). Download it, stop the site in the panel, upload the files with
FTP over the old ones (keep `App_Data` and the settings file), and start the site again.

## 4. After the first production deploy

1. Open `https://towerhaat.com`, sign in with your number (listed in `AdminPhones`): you are the
   platform admin.
2. **Admin → Societies → Ace Divino → Bulk upload**: paste the tower and flat list (`Tower,Flat`
   per line).
3. **Admin → Backups → Back up now**, then **Download**. Do this every week and keep the files
   in Google Drive. They hold residents' data: keep them private.
4. Also back up `App_Data/uploads` (business and menu photos) from the File Manager now and then.
5. Check **Admin → Error log** after the first day.

## What lives where on the server

| Path | What | Deploys |
| --- | --- | --- |
| `appsettings.Production.json` | secrets and settings | never touched |
| `App_Data/uploads` | photos | never touched |
| `App_Data/logs` | `app-*.log` (14 days), `errors-*.log` (60 days), `startup-error.log` | never touched |
| `App_Data/backups` | nightly database backups (14 days) | never touched |
| everything else | the app | replaced |
