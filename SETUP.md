# Void Stunts: from this folder to TestFlight

## One click (Windows PC): play in Safari

Unzip the whole folder and double-click **DEPLOY.bat**. It will:
1. install Git and the GitHub CLI if they're missing
2. sign you in to GitHub in your browser, or let you create an account
3. ask for your Unity login (and open id.unity.com if you need to create one) and store it as encrypted GitHub secrets
4. create a public repository, turn on GitHub Pages, upload the project and start the cloud build
5. wait for the build (about 45–60 minutes the first time) and open the game link, with a QR code for your iPhone

To update later, run it again; it reuses the same repository. If a build fails, it saves `build-error-log.txt`. Send that file to Claude.

The rest of this document is the manual route, plus the later steps for TestFlight once you join Apple's program.
When you have joined, add the Apple secrets below and set the repository variable **APPLE_READY** = `true`. Until then the iOS workflow is skipped.


You don't need a Mac for this. GitHub's cloud Macs build the app and upload it to TestFlight. You do the one-time setup below; after that, each change you push produces a new TestFlight build about 40 minutes later.

You can expect it to take around an hour of clicking, most of it spent waiting for Apple to approve your enrollment.

---

## 0. Free first: play it in Safari (no Apple account needed)

The same game, built for the browser and hosted free on GitHub Pages. This lets you check the levels, scale, looks and touch controls on your iPhone before paying Apple.
The browser version has no sound or vibration, and it runs a bit slower than the real app.

1. Do **step 4** (Unity account) and **step 5** (GitHub repo). For step 5, only the two `UNITY_…` secrets are needed.
2. In the repo, go to **Settings → Pages → Build and deployment → Source** and choose **GitHub Actions**.
3. Go to **Actions → Web preview → Run workflow**. The first run takes about 40–60 minutes.
4. When it's green, open `https://<your-github-name>.github.io/void-stunts/` in Safari on your iPhone.
   Tap Share, then **Add to Home Screen**. It then opens full-screen like an app.

The iOS → TestFlight workflow will fail (red) until you've done steps 1–3. That's expected, and you can ignore it until then.

## 1. Apple Developer Program ($99/year)

1. Go to **developer.apple.com/programs/enroll** and sign in with your Apple ID. Enroll as an **Individual**.
2. Approval can take anywhere from a few minutes to about 48 hours. You can't continue until you get the "Welcome" email.
3. Once you're approved, open **developer.apple.com/account → Membership details** and copy your **Team ID** (10 characters, e.g. `A1B2C3D4E5`).

## 2. Create the app in App Store Connect

1. Go to **appstoreconnect.apple.com → Apps → + → New App**.
   - Platform: iOS · Name: *Void Stunts* (if it's taken, add a word) · Primary language: English
   - Bundle ID: click *Register a new bundle ID* (this opens the developer site). Choose *Explicit* and enter something like `com.yourname.voidstunts`. Return to the form and select it.
   - SKU: `voidstunts`
2. Open the app, go to **TestFlight → Internal Testing → +**, and create a group called `Team`. Add yourself.

## 3. App Store Connect API key (lets the cloud Mac sign and upload)

1. Go to **appstoreconnect.apple.com → Users and Access → Integrations → App Store Connect API → Team Keys → +**.
   Name: `CI`. Access: **App Manager**. Then click Generate.
2. Download the `AuthKey_XXXXXXXXXX.p8` file. **You can only download it once**, so keep it safe.
3. Copy the **Key ID** (shown in the row) and the **Issuer ID** (shown above the table).
4. Convert the .p8 file to one line of text. On a Mac, open Terminal and run:
   ```
   base64 -i ~/Downloads/AuthKey_XXXXXXXXXX.p8 | pbcopy
   ```
   The text is now on your clipboard.

## 4. Unity account (free Personal licence)

1. Create an account at **id.unity.com** if you don't already have one.
2. **Turn off 2-step verification** on this account. The cloud build can't type in a code. If you want to keep 2FA on your main account, create a separate one for the builds.
3. Sign in once at **unity.com** and accept the Personal licence terms.

## 5. GitHub repository

1. Create a free account at **github.com** and click **New repository**. Name it `void-stunts`.
   - **Public** repos get unlimited free Mac build minutes.
   - **Private** repos get 2,000 free minutes a month, but Mac minutes count ×10. That works out to about 4–5 builds a month, unless you upgrade to GitHub Pro.
2. Upload this folder:
   - Easiest: on the repo page, click *uploading an existing file* and drag in **everything inside** the `VoidStunts` folder.
     In Finder, first press **⌘ ⇧ .** to show hidden files so that the `.github` folder is included. Without it, nothing builds.
   - Alternatively, with GitHub Desktop: *Add Existing Repository*, choose the folder, then Publish.
3. Go to **Settings → Secrets and variables → Actions** and add the following.

   **Secrets** (tab *Secrets → New repository secret*):
   | Name | Value |
   |---|---|
   | `UNITY_USERNAME` | your Unity email |
   | `UNITY_PASSWORD` | your Unity password |
   | `APPLE_TEAM_ID` | Team ID from step 1 |
   | `APP_STORE_CONNECT_KEY` | the base64 text from step 3 |
   | `APP_STORE_CONNECT_KEY_ID` | Key ID |
   | `APP_STORE_CONNECT_ISSUER_ID` | Issuer ID |

   **Variables** (tab *Variables → New repository variable*):
   | Name | Value |
   |---|---|
   | `BUNDLE_ID` | exactly the bundle ID from step 2 |
   | `TESTFLIGHT_GROUP` | `Team` |

## 6. Build

1. Go to **Actions → iOS → TestFlight → Run workflow**.
2. The first run takes 45–70 minutes, because it downloads Unity and imports everything. Later runs reuse a cache.
3. When it turns green, App Store Connect processes the build for another 5–15 minutes. After that, install **TestFlight** from the App Store on your iPhone and open the invite email.

From then on, every push to `main` builds automatically.

## If a run fails

- Open the red run, click the failed step and scroll to the first `error`. Paste that text into our chat and I'll fix it.
- *"Unity license activation failed"*: 2-step verification is still on, or the Personal terms haven't been accepted (see step 4).
- *"No profiles for bundle id"*: the `BUNDLE_ID` variable doesn't match the app in App Store Connect.
- *"Missing compliance"*: this is already handled (the app declares no non-exempt encryption).

## Controls (same as the web version)

- **Left thumb (body):** slide to steer · flick up to hop · in the air, slide or flick to flip/roll · hold still to catch
- **Right thumb (power):** hold for gas · slide up for rocket, down to brake · flick up for a burst, down to drift · in the air, slide sideways to spin
